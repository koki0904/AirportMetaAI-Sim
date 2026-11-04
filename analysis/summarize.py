"""バッチ実験の結果を集計し、KPI の比較表と図を出す。

使い方（プロジェクトのルートで）:
    python analysis/summarize.py Results/pilot
    python analysis/summarize.py Results/main
引数を省略すると Results/ を読む。
入力: <dir>/summary_*.csv（1実行1ファイル。無ければ <dir>/summary.csv）
出力: <dir>/kpi_table.csv（平均・95%信頼区間・件数）、<dir>/kpi_comparison.png
必要: pandas, matplotlib, scipy
"""
import sys
from pathlib import Path

import matplotlib.pyplot as plt
import pandas as pd
from scipy import stats

ROOT = Path(__file__).resolve().parent.parent
KPIS = [
    ("avgWait", "Average wait (s)"),
    ("peakDensity", "Peak density (persons/m2)"),
    ("avgWaitHighFatigue", "Wait of high-fatigue passengers (s)"),
]
POLICIES = ["NoRobot", "FixedRule", "MetaAI"]
COLORS = {"NoRobot": "#9e9e9e", "FixedRule": "#5b8def", "MetaAI": "#e8743b"}


def load(d: Path) -> pd.DataFrame:
    files = sorted(d.glob("summary_*.csv"))
    if files:
        df = pd.concat([pd.read_csv(f) for f in files], ignore_index=True)
    else:
        df = pd.read_csv(d / "summary.csv")
    return df.drop_duplicates(subset=["policy", "scenario", "seed"], keep="last")


def ci95(x: pd.Series) -> float:
    x = x.dropna()
    if len(x) < 2:
        return float("nan")
    return float(stats.t.ppf(0.975, len(x) - 1) * x.std(ddof=1) / len(x) ** 0.5)


def main() -> None:
    d = Path(sys.argv[1]) if len(sys.argv) > 1 else ROOT / "Results"
    df = load(d)
    df["completionRate"] = df["completed"] / df["spawned"]

    rows = []
    for (sc, pol), g in df.groupby(["scenario", "policy"]):
        r = {"scenario": sc, "policy": pol, "n": len(g), "completionRate": g["completionRate"].mean()}
        for col, _ in KPIS:
            r[col + "_mean"] = g[col].mean()
            r[col + "_ci95"] = ci95(g[col])
        rows.append(r)
    table = pd.DataFrame(rows)
    table.to_csv(d / "kpi_table.csv", index=False)
    print(table.round(3).to_string(index=False))

    # MetaAI と FixedRule の対応のある比較（同じシード同士）
    for sc in sorted(df["scenario"].unique()):
        a = df[(df.scenario == sc) & (df.policy == "MetaAI")].set_index("seed")
        b = df[(df.scenario == sc) & (df.policy == "FixedRule")].set_index("seed")
        common = a.index.intersection(b.index)
        if len(common) < 5:
            continue
        for col, _ in KPIS:
            x, y = a.loc[common, col].dropna(), b.loc[common, col].dropna()
            idx = x.index.intersection(y.index)
            if len(idx) < 5:
                continue
            res = stats.wilcoxon(x[idx], y[idx])
            diff = (x[idx] - y[idx]).mean()
            print(f"[{sc}] {col}: MetaAI - FixedRule = {diff:+.3f} (n={len(idx)}, Wilcoxon p={res.pvalue:.4f})")

    scenarios = sorted(df["scenario"].unique())
    fig, axes = plt.subplots(len(scenarios), len(KPIS), figsize=(4.2 * len(KPIS), 3.4 * len(scenarios)), squeeze=False)
    for r, sc in enumerate(scenarios):
        t = table[table.scenario == sc].set_index("policy").reindex(POLICIES)
        for c, (col, label) in enumerate(KPIS):
            ax = axes[r][c]
            ax.bar(t.index, t[col + "_mean"], yerr=t[col + "_ci95"], capsize=4,
                   color=[COLORS[p] for p in t.index])
            ax.set_title(f"{sc}: {label}", fontsize=10)
            ax.tick_params(axis="x", labelsize=9)
    fig.suptitle("Mean with 95% CI", fontsize=10)
    fig.tight_layout()
    out = d / "kpi_comparison.png"
    fig.savefig(out, dpi=200)
    print(f"saved: {out}")


if __name__ == "__main__":
    main()
