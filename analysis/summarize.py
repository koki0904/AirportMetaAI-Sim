"""Results/summary.csv を集計し、3つのKPIの比較棒グラフを Results/kpi_comparison.png に保存する。

使い方（プロジェクトのルートで）:
    python analysis/summarize.py
必要: pandas, matplotlib
"""
from pathlib import Path

import matplotlib.pyplot as plt
import pandas as pd

ROOT = Path(__file__).resolve().parent.parent
SUMMARY = ROOT / "Results" / "summary.csv"
OUT = ROOT / "Results" / "kpi_comparison.png"

KPIS = [
    ("avgWait", "Average wait (s)"),
    ("peakDensity", "Peak density (persons/m2)"),
    ("avgWaitHighFatigue", "Wait of high-fatigue passengers (s)"),
]
POLICIES = ["NoRobot", "FixedRule", "MetaAI"]


def main() -> None:
    df = pd.read_csv(SUMMARY)
    stats = df.groupby(["scenario", "policy"])[[k for k, _ in KPIS]].agg(["mean", "std", "count"])
    print(stats.round(3).to_string())

    scenarios = sorted(df["scenario"].unique())
    fig, axes = plt.subplots(len(scenarios), len(KPIS), figsize=(4 * len(KPIS), 3.2 * len(scenarios)), squeeze=False)
    for r, sc in enumerate(scenarios):
        for c, (col, label) in enumerate(KPIS):
            ax = axes[r][c]
            sub = df[df["scenario"] == sc].groupby("policy")[col].agg(["mean", "std"]).reindex(POLICIES)
            ax.bar(sub.index, sub["mean"], yerr=sub["std"], capsize=3)
            ax.set_title(f"{sc}: {label}", fontsize=9)
            ax.tick_params(axis="x", labelsize=8)
    fig.tight_layout()
    fig.savefig(OUT, dpi=200)
    print(f"saved: {OUT}")


if __name__ == "__main__":
    main()
