"""B1 poster draft built on the university template (python-pptx).
Usage: python build_poster.py template.pptx out.pptx [figA.png figB.png]
"""
import sys
from copy import deepcopy

from lxml import etree
from pptx import Presentation
from pptx.dml.color import RGBColor
from pptx.enum.shapes import MSO_SHAPE, MSO_CONNECTOR
from pptx.enum.text import MSO_ANCHOR, PP_ALIGN
from pptx.oxml.ns import qn
from pptx.util import Emu, Pt

DIR = sys.argv[0].rsplit("/", 1)[0] if "/" in sys.argv[0] else "."
SRC, OUT = sys.argv[1], sys.argv[2]
FIG_A = sys.argv[3] if len(sys.argv) > 3 else None
FIG_B = sys.argv[4] if len(sys.argv) > 4 else None

DARK = RGBColor(0x0C, 0x3A, 0x14)     # body text (template level-2 colour)
GREEN = RGBColor(0x1C, 0x86, 0x2E)    # headings (template level-1 colour)
WHITE = RGBColor(0xFF, 0xFF, 0xFF)
META = RGBColor(0xE8, 0x74, 0x3B)     # Meta-AI accent (same as charts)
GREY = RGBColor(0x6B, 0x6B, 0x6B)
TODO = RGBColor(0xC6, 0x28, 0x28)

BODY, SUB, HEAD = 34, 30, 42          # pt


def mm(v):
    return Emu(int(v * 36000))


prs = Presentation(SRC)
s = prs.slides[0]


def set_bullet(p, on):
    pPr = p._p.get_or_add_pPr()
    for tag in ("a:buNone", "a:buChar", "a:buFont"):
        for el in pPr.findall(qn(tag)):
            pPr.remove(el)
    if on:
        pPr.set("marL", str(mm(9)))
        pPr.set("indent", str(-mm(9)))
        f = etree.SubElement(pPr, qn("a:buFont"))
        f.set("typeface", "Arial")
        c = etree.SubElement(pPr, qn("a:buChar"))
        c.set("char", "•")
    else:
        etree.SubElement(pPr, qn("a:buNone"))


def text(x, y, w, h, paras, anchor=MSO_ANCHOR.TOP, fill=None, line=None, name=None, align=PP_ALIGN.LEFT, margin=2.5):
    """paras: list of dict(t, size, bold, color, bullet, after) or list of runs under key 'runs'."""
    if fill is None and line is None:
        sh = s.shapes.add_textbox(mm(x), mm(y), mm(w), mm(h))
    else:
        sh = s.shapes.add_shape(MSO_SHAPE.ROUNDED_RECTANGLE, mm(x), mm(y), mm(w), mm(h))
        sh.adjustments[0] = 0.06
        sh.shadow.inherit = False
        if fill:
            sh.fill.solid(); sh.fill.fore_color.rgb = fill
        else:
            sh.fill.background()
        if line:
            sh.line.color.rgb = line; sh.line.width = Pt(3)
        else:
            sh.line.fill.background()
    if name:
        sh.name = name
    tf = sh.text_frame
    tf.word_wrap = True
    tf.vertical_anchor = anchor
    for side in ("margin_left", "margin_right", "margin_top", "margin_bottom"):
        setattr(tf, side, mm(margin))
    first = True
    for pd in paras:
        p = tf.paragraphs[0] if first else tf.add_paragraph()
        first = False
        p.alignment = pd.get("align", align)
        runs = pd.get("runs") or [dict(t=pd["t"])]
        for rd in runs:
            r = p.add_run()
            r.text = rd["t"]
            f = r.font
            f.size = Pt(rd.get("size", pd.get("size", BODY)))
            f.bold = rd.get("bold", pd.get("bold", False))
            f.color.rgb = rd.get("color", pd.get("color", DARK))
        set_bullet(p, pd.get("bullet", False))
        if pd.get("after") is not None:
            p.space_after = Pt(pd["after"])
    return sh


def box(x, y, w, h, fill=WHITE, line=None, name=None):
    sh = s.shapes.add_shape(MSO_SHAPE.ROUNDED_RECTANGLE, mm(x), mm(y), mm(w), mm(h))
    sh.adjustments[0] = 0.05
    sh.shadow.inherit = False
    sh.fill.solid(); sh.fill.fore_color.rgb = fill
    if line:
        sh.line.color.rgb = line; sh.line.width = Pt(3)
    else:
        sh.line.fill.background()
    if name:
        sh.name = name
    return sh


def arrow(x1, y1, x2, y2, color=GREEN, width=6):
    c = s.shapes.add_connector(MSO_CONNECTOR.STRAIGHT, mm(x1), mm(y1), mm(x2), mm(y2))
    c.line.color.rgb = color
    c.line.width = Pt(width)
    ln = c.line._get_or_add_ln()
    tail = etree.SubElement(ln, qn("a:tailEnd"))
    tail.set("type", "triangle"); tail.set("w", "med"); tail.set("len", "med")
    return c


def picture(path, x, y, w=None, h=None, name=None):
    pic = s.shapes.add_picture(path, mm(x), mm(y), mm(w) if w else None, mm(h) if h else None)
    if name:
        pic.name = name
    return pic


def placeholder_fig(x, y, w, h, label, name):
    sh = box(x, y, w, h, fill=WHITE, line=GREY, name=name)
    sh.line.dash_style = 4  # dash
    tf = sh.text_frame; tf.word_wrap = True; tf.vertical_anchor = MSO_ANCHOR.MIDDLE
    for i, line in enumerate(label.split("\n")):
        p = tf.paragraphs[0] if i == 0 else tf.add_paragraph()
        p.alignment = PP_ALIGN.CENTER
        r = p.add_run(); r.text = line
        r.font.size = Pt(SUB); r.font.color.rgb = GREY
    return sh


# ---------------- title and section headings (template placeholders) ----------------
ph = {p.placeholder_format.idx: p for p in s.placeholders}
title = s.shapes.title
tp = title.text_frame.paragraphs[0]
tp.runs[0].text = "空港を都市の縮図としたMeta-AIによるロボット戦術選択"
tp.runs[0].font.size = Pt(60)
tp.alignment = PP_ALIGN.CENTER
sub = deepcopy(tp._p)
title.text_frame._txBody.append(sub)
sp = title.text_frame.paragraphs[1]
sp.runs[0].text = "― 旅客の見えない疲労を考慮した支援に向けて ―"
sp.runs[0].font.size = Pt(38)
sp.alignment = PP_ALIGN.CENTER
title.text_frame.vertical_anchor = MSO_ANCHOR.MIDDLE

for idx, head in ((10, "研究の背景と目的"), (11, "手法と実験"), (12, "結果と今後の展望")):
    tf = ph[idx].text_frame
    for extra in tf.paragraphs[1:]:
        tf._txBody.remove(extra._p)
    tf.paragraphs[0].runs[0].text = head

text(36, 98.5, 655, 17, [dict(t="兒玉 弘毅（立教大学大学院 人工知能科学研究科 M1・三宅研究室）", size=34, bold=True, align=PP_ALIGN.RIGHT)],
     anchor=MSO_ANCHOR.MIDDLE, margin=0, name="Author")

# ---------------- box 1: background (y 117-397) ----------------
Y1 = 117
text(46, Y1 + 30, 385, 140, [
    dict(t="都市には、効率だけでなく人が快適に過ごせる体験価値が求められている", bullet=True, after=14),
    dict(t="空港は人流・商業・防災・多言語案内が狭い空間に集まる「スマートシティの縮図」", bullet=True, after=14),
    dict(t="案内ロボットは増えているが、多くは決められた仕事をこなすだけで、状況に応じた役割の切り替えができていない", bullet=True),
], name="BackgroundText")

# right: airport = miniature city diagram
cx, cy = 437, Y1 + 30
box(cx, cy, 244, 140, fill=WHITE, name="MiniCityPanel")
core = text(cx + 72, cy + 50, 100, 40, [dict(t="空港", size=44, bold=True, color=WHITE, align=PP_ALIGN.CENTER)],
            anchor=MSO_ANCHOR.MIDDLE, fill=GREEN, name="AirportCore")
for i, (lab, bx, by) in enumerate((("人流", cx + 8, cy + 8), ("商業", cx + 176, cy + 8),
                                   ("防災", cx + 8, cy + 98), ("多言語案内", cx + 156, cy + 98))):
    wbox = 80 if i == 3 else 60
    text(bx, by, wbox, 34, [dict(t=lab, size=SUB, bold=True, color=GREEN, align=PP_ALIGN.CENTER)],
         anchor=MSO_ANCHOR.MIDDLE, fill=RGBColor(0xE6, 0xF7, 0xE9), name="Func" + str(i))
text(cx + 40, cy + 92, 164, 12, [dict(t="都市の機能が凝縮", size=26, color=GREY, align=PP_ALIGN.CENTER)],
     margin=0, name="MiniCityCaption")
for (x1, y1, x2, y2) in ((cx + 68, cy + 42, cx + 82, cy + 52), (cx + 176, cy + 42, cx + 162, cy + 52),
                         (cx + 68, cy + 98, cx + 82, cy + 88), (cx + 176, cy + 98, cx + 162, cy + 88)):
    arrow(x1, y1, x2, y2, width=4)

text(46, Y1 + 180, 312, 92, [
    dict(t="目的", size=HEAD, bold=True, color=GREEN, after=4),
    dict(t="空港全体を見渡すMeta-AIが、混雑や旅客の状態に応じて複数ロボットの戦術を選ぶ仕組みをつくる", size=38),
], fill=WHITE, name="Purpose", margin=6)
text(369, Y1 + 180, 312, 92, [
    dict(t="問い", size=HEAD, bold=True, color=META, after=4),
    dict(t="旅客の疲労が外から見えないとき、ロボットは「助ける」と「確かめる」をどう配分すべきか", size=38),
], fill=WHITE, line=META, name="Question", margin=6)

# ---------------- box 2: method (y 416-696) ----------------
Y2 = 416
text(46, Y2 + 30, 270, 12, [dict(t="提案：Meta-AIによる階層型の意思決定", size=SUB, bold=True, color=GREEN)], margin=0, name="HierCaption")
layers = [
    ("Meta-AI", "空港全体を観測し、戦術を1つ選ぶ", META),
    ("戦術", "待ち行列の平準化 ／ 疲労者の優先支援", GREEN),
    ("キャラクターAI", "各ロボットが戦術に沿って行動", GREEN),
    ("ナビゲーションAI", "経路を計算して移動（NavMesh）", GREEN),
]
ly = Y2 + 46
for i, (name_, desc, col) in enumerate(layers):
    text(46, ly, 270, 44, [dict(t=name_, size=38, bold=True, color=WHITE, after=0),
                           dict(t=desc, size=26, color=WHITE)],
         anchor=MSO_ANCHOR.MIDDLE, fill=col, name="Layer" + str(i), margin=4)
    if i < len(layers) - 1:
        arrow(181, ly + 44, 181, ly + 56)
    ly += 56

# right column
RX, RW = 330, 351
pic = picture(DIR + "/unity_view.png", RX, Y2 + 30, w=150, name="UnityView")
text(RX + 156, Y2 + 28, RW - 156, 72, [
    dict(t="Unityで最小の空港を再現", size=SUB, bold=True, color=GREEN, after=4),
    dict(t="入口→チェックイン×2→保安検査×2→商業→ゲート", size=26, after=4),
    dict(t="旅客は疲労・ストレス・時間の余裕を持つ", size=26),
], margin=0, name="SimText")

text(RX, Y2 + 104, RW, 82, [
    dict(t="比較実験", size=SUB, bold=True, color=GREEN, after=4),
    dict(runs=[dict(t="方針：", bold=True), dict(t="ロボット無し／固定ルール／Meta-AI")], size=28, bullet=True, after=2),
    dict(runs=[dict(t="条件：", bold=True), dict(t="通常（23人/分）・混雑（30人/分）× 30シード")], size=28, bullet=True, after=2),
    dict(runs=[dict(t="評価：", bold=True), dict(t="平均待ち時間、ピーク混雑密度、高疲労旅客の待ち時間")], size=28, bullet=True),
], fill=WHITE, name="Experiment", margin=5)

text(RX, Y2 + 191, RW, 82, [
    dict(t="目標とする定式化", size=SUB, bold=True, color=GREEN, after=40),
    dict(t=" ", size=8, after=34),
    dict(t="「助ける効果」＋「確かめる効果」−「動くコスト」が最大の行動を選ぶ", size=24, after=2),
    dict(t="※現在の実装：疲労が見える前提で λ=0 の場合をルールで近似", size=22, color=META),
], fill=WHITE, name="Formulation", margin=5)
picture(DIR + "/formula.png", RX + 8, Y2 + 211, w=RW - 16, name="FormulaImage")

# ---------------- box 3: results & outlook (y 715-995) ----------------
Y3 = 715
if FIG_A:
    picture(FIG_A, 46, Y3 + 30, w=200, name="FigA")
else:
    placeholder_fig(46, Y3 + 30, 200, 128, "図A：3方針×2シナリオの平均待ち時間\n（95%信頼区間）\n実験後に差し替え", "FigA")
if FIG_B:
    picture(FIG_B, 252, Y3 + 30, w=200, name="FigB")
else:
    placeholder_fig(252, Y3 + 30, 200, 128, "図B：Meta-AIの戦術切替と\n行列長の推移\n実験後に差し替え", "FigB")

text(46, Y3 + 164, 406, 108, [
    dict(t="所見（各条件30シード、Wilcoxon符号順位検定）", size=SUB, bold=True, color=GREEN, after=4),
    dict(runs=[dict(t="通常：", bold=True), dict(t="Meta-AIは高疲労旅客の待ち時間を固定ルールより約21%短縮（14.0→11.0秒、p=0.006）。ロボット無しとの差は有意でない（p=0.08）")], size=27, bullet=True, after=4),
    dict(runs=[dict(t="安定性：", bold=True), dict(t="平均待ち時間は3方針で同程度だが、Meta-AIはシード間のばらつきが最小（SD 2.2秒、固定ルール4.9秒）")], size=27, bullet=True, after=4),
    dict(runs=[dict(t="混雑：", bold=True), dict(t="待ち時間の差は消え、ピーク密度のみ低下（0.45→0.42人/㎡、p=0.04）。全レーンが詰まると平準化が効かない")], size=27, bullet=True),
], fill=WHITE, name="Findings", margin=5)

# roadmap
RX3, RW3 = 462, 219
text(RX3, Y3 + 28, RW3, 12, [dict(t="今後のロードマップ", size=SUB, bold=True, color=GREEN)], margin=0, name="RoadmapTitle")
steps = [
    ("2026 冬", "効用関数とスコア型Meta-AI、軽量なPython環境", GREEN),
    ("2027 春", "疲労を隠して信念推定、「確かめる」行動を導入（人工知能学会で発表予定）", GREEN),
    ("2027 秋〜", "強化学習で戦術選択を学習、国際学会へ投稿", META),
]
ry = Y3 + 44
for i, (when, what, col) in enumerate(steps):
    text(RX3, ry, 52, 40, [dict(t=when, size=24, bold=True, color=WHITE, align=PP_ALIGN.CENTER)],
         anchor=MSO_ANCHOR.MIDDLE, fill=col, name="StepWhen" + str(i), margin=1)
    text(RX3 + 55, ry, RW3 - 55, 40, [dict(t=what, size=26)], anchor=MSO_ANCHOR.MIDDLE, fill=WHITE,
         name="StepWhat" + str(i), margin=3)
    ry += 45

text(RX3, Y3 + 182, RW3, 90, [
    dict(t="相談したいこと", size=SUB, bold=True, color=META, after=4),
    dict(t="見えない疲労を推定する方法は？", size=24, bullet=True, after=2),
    dict(t="評価は待ち時間と疲労のどちらを重視？", size=24, bullet=True, after=2),
    dict(t="空港の人流で使える公開データは？", size=24, bullet=True),
], fill=WHITE, line=META, name="Consult", margin=5)

prs.save(OUT)
print("saved", OUT)
