import sys
import numpy as np, pandas as pd
import matplotlib; matplotlib.use("Agg")
import matplotlib.pyplot as plt
from matplotlib import font_manager as fm
from scipy import stats

CJK = [f.name for f in fm.fontManager.ttflist if "Noto Sans CJK JP" in f.name]
plt.rcParams["font.family"] = CJK[0] if CJK else "sans-serif"
plt.rcParams["axes.spines.top"] = False; plt.rcParams["axes.spines.right"] = False
INK, MUTED, GRID = "#1f2a22", "#5f6b62", "#dfe5e0"
POL = ["NoRobot", "FixedRule", "MetaAI"]
POL_JA = {"NoRobot": "ロボット無し", "FixedRule": "固定ルール", "MetaAI": "Meta-AI"}
COL = {"NoRobot": "#8c8c8c", "FixedRule": "#3b6fd8", "MetaAI": "#e8743b"}
SC = [("Normal", "通常"), ("Congested", "混雑")]
MM = 1 / 25.4

df = pd.read_csv(sys.argv[1])
out = sys.argv[2] if len(sys.argv) > 2 else "."

def ci(x):
    x = np.asarray(x); return stats.t.ppf(.975, len(x) - 1) * x.std(ddof=1) / np.sqrt(len(x))

def pval(sc, col, a="MetaAI", b="FixedRule"):
    A = df[(df.scenario == sc) & (df.policy == a)].set_index("seed")[col]
    B = df[(df.scenario == sc) & (df.policy == b)].set_index("seed")[col]
    i = A.index.intersection(B.index); return stats.wilcoxon(A[i], B[i]).pvalue

for sc, _ in SC:
    for col in ["avgWait", "avgWaitHighFatigue", "peakDensity"]:
        print(sc, col, "MetaAI vs FixedRule p=%.4f" % pval(sc, col), "MetaAI vs NoRobot p=%.4f" % pval(sc, col, b="NoRobot"))

# ---- Fig A: two KPIs, grouped by scenario ----
fig, axes = plt.subplots(1, 2, figsize=(200 * MM, 128 * MM), dpi=300)
for ax, (col, title) in zip(axes, [("avgWait", "平均待ち時間"), ("avgWaitHighFatigue", "高疲労旅客の待ち時間")]):
    w = 0.26
    for j, pol in enumerate(POL):
        xs, ms, es = [], [], []
        for i, (sc, _) in enumerate(SC):
            v = df[(df.scenario == sc) & (df.policy == pol)][col]
            xs.append(i + (j - 1) * (w + 0.02)); ms.append(v.mean()); es.append(ci(v))
        ax.bar(xs, ms, w, color=COL[pol], label=POL_JA[pol], zorder=2)
        ax.errorbar(xs, ms, yerr=es, fmt="none", ecolor=INK, elinewidth=1.4, capsize=4, zorder=3)
        for x, m, e in zip(xs, ms, es):
            ax.text(x, m + e + 1.2, f"{m:.1f}", ha="center", va="bottom", fontsize=11, color=INK)
    ax.set_xticks(range(len(SC))); ax.set_xticklabels([f"{ja}シナリオ" for _, ja in SC], fontsize=14, color=INK)
    ax.set_title(title, fontsize=16, color=INK, pad=8, fontweight="bold")
    ax.set_ylabel("秒（平均と95%信頼区間）", fontsize=12, color=MUTED)
    ax.tick_params(axis="y", labelsize=11, colors=MUTED); ax.yaxis.grid(True, color=GRID, zorder=0)
    ax.set_ylim(0, 72)
    ax.spines["left"].set_color(GRID); ax.spines["bottom"].set_color(MUTED)
# significance marker: Normal high-fatigue, MetaAI vs FixedRule
ax = axes[1]
p = pval("Normal", "avgWaitHighFatigue")
x1, x2 = 0 + 0 * (0.28), 0 + 1 * 0.28
y = 22
ax.plot([x1, x1, x2, x2], [y, y + 1.5, y + 1.5, y], color=INK, lw=1.3)
ax.text((x1 + x2) / 2, y + 2, f"p={p:.3f}", ha="center", va="bottom", fontsize=11, color=INK)
h, l = axes[0].get_legend_handles_labels()
fig.legend(h, l, loc="upper center", ncol=3, fontsize=13, frameon=False, bbox_to_anchor=(0.5, 1.0))
fig.tight_layout(rect=(0, 0, 1, 0.92))
fig.savefig(f"{out}/figA_wait.png"); plt.close(fig)

# ---- Fig B: tactic share (left) + spread of avg wait in normal scenario (right) ----
fig, axes = plt.subplots(1, 2, figsize=(200 * MM, 128 * MM), dpi=300, gridspec_kw=dict(width_ratios=[1, 1.15]))
ax = axes[0]
TAC = [("tacticQueueEq", "待ち行列の平準化", "#2a9d8f"), ("tacticFatigueFirst", "疲労者の優先支援", "#8e5bd6"), ("tacticNone", "介入なし", "#c9cfca")]
m = df[df.policy == "MetaAI"].groupby("scenario")[[t for t, _, _ in TAC]].sum()
for i, (sc, ja) in enumerate(SC):
    tot = m.loc[sc].sum(); left = 0
    for t, lab, c in TAC:
        share = m.loc[sc, t] / tot * 100
        ax.barh(i, share, left=left, color=c, edgecolor="white", linewidth=2, height=0.55, label=lab if i == 0 else None)
        if share > 10:
            ax.text(left + share / 2, i, f"{share:.0f}%", ha="center", va="center", fontsize=12, color="white" if c != "#c9cfca" else INK, fontweight="bold")
        left += share
ax.set_yticks(range(len(SC))); ax.set_yticklabels([ja for _, ja in SC], fontsize=14, color=INK); ax.invert_yaxis()
ax.set_xlim(0, 100); ax.set_xlabel("Meta-AIが選んだ戦術の割合（%）", fontsize=12, color=MUTED)
ax.tick_params(axis="x", labelsize=10, colors=MUTED)
ax.set_title("状況に応じた戦術の切替", fontsize=16, color=INK, fontweight="bold", pad=8)
ax.legend(loc="upper center", bbox_to_anchor=(0.5, -0.2), ncol=2, fontsize=11, frameon=False, columnspacing=1.0)
for s in ("left", "bottom"): ax.spines[s].set_color(GRID)

ax = axes[1]
data = [df[(df.scenario == "Normal") & (df.policy == p)]["avgWait"].values for p in POL]
bp = ax.boxplot(data, widths=0.5, patch_artist=True, medianprops=dict(color=INK, lw=1.6), whiskerprops=dict(color=MUTED), capprops=dict(color=MUTED), flierprops=dict(marker="o", markersize=5, markerfacecolor=MUTED, markeredgecolor="none"))
for patch, p in zip(bp["boxes"], POL):
    patch.set_facecolor(COL[p]); patch.set_edgecolor("white")
ax.set_xticks([1, 2, 3]); ax.set_xticklabels([POL_JA[p] for p in POL], fontsize=13, color=INK)
ax.set_ylabel("平均待ち時間（秒）", fontsize=12, color=MUTED)
ax.tick_params(axis="y", labelsize=11, colors=MUTED); ax.yaxis.grid(True, color=GRID)
ax.set_title("結果のばらつき（通常・30シード）", fontsize=16, color=INK, fontweight="bold", pad=8)
ax.set_ylim(5, 37)
for i, d in enumerate(data):
    ax.text(i + 1, 36.5, f"SD {d.std(ddof=1):.1f}", ha="center", va="top", fontsize=11, color=INK)
for s in ("left", "bottom"): ax.spines[s].set_color(GRID)
fig.tight_layout()
fig.savefig(f"{out}/figB_tactics.png"); plt.close(fig)
print("ok")
