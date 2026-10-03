# AirportMetaAI-Sim

修士研究「空港を未来都市の縮図としたMeta-AIによるロボット戦術生成」のシミュレーション環境。
当面の目標は **2026/11/10 のポスター発表** に載せる最小構成の比較実験（凍結日: 2026/10/31）。

## 環境
- Unity 6.4（6000.4.11f1）、URP、AI Navigation 2.0（NavMeshSurface / NavMeshAgent）、Windows
- C# は名前空間 `AirportSim`。Python（pandas/matplotlib）は `analysis/` で集計・作図のみに使う

## 研究の問い（ポスターで答える範囲）
- 同じ旅客流に対し、**固定ルール運用** と **Meta-AI の戦術切替** で、待ち時間・混雑・高疲労者の待ち時間がどう変わるか
- 強化学習は今回やらない。Meta-AI v0 はルールベース（学会向けの次段階でRLに置き換える）

## 用語
- **Meta-AI**: 空港全体の状態を観測し、一定間隔で「戦術」を1つ選ぶ上位AI（`MetaAIController`）
- **戦術（Tactic）**: Meta-AI が選ぶ運用方針。v0 は2つ
  - `QueueEqualization`（混雑平準化）: 並列レーンの待ち行列が偏ったとき、旅客を短いレーンへ誘導
  - `FatigueFirstAssistance`（疲労優先支援）: 高疲労の旅客をロボットが優先支援（優先サービス＋疲労軽減）
- **Human State**: 旅客の状態（疲労 fatigue、ストレス stress、時間余裕 timeSlack。いずれも 0〜1）
- **Policy（比較条件）**: `NoRobot` / `FixedRule` / `MetaAI`

## シミュレーション構成
- ゾーン: Entrance → CheckIn（並列2レーン）→ Security（並列2レーン）→ Shopping（時間余裕が大きい旅客のみ）→ Gate
- 旅客は NavMeshAgent で移動し、ゾーンの待ち行列に並ぶ。ゾーンは `servers` 数と `serviceTime` で処理する
- ロボットは案内ロボット1種類（`GuideRobot`）

## KPI（定義を勝手に変えない）
1. 平均待ち時間: 完了した旅客の待ち行列での待機時間（秒）の平均
2. ピーク混雑率: CheckIn / Security ゾーンの人口密度（人/㎡）の最大値
3. 高疲労者の待ち時間: 開始時の疲労が `highFatigueThreshold` 以上の旅客の平均待ち時間
- 出力は `Results/summary.csv`（1実行1行）と `Results/passengers_*.csv`。コミットしない

## 実験設計
- 3条件（NoRobot / FixedRule / MetaAI）× 2シナリオ（Normal / Congested）× 30シード = 180回（仮置き）
- コマンドライン上書き: `-policy MetaAI -scenario Congested -seed 3 -duration 600 -timescale 20`
- 未決事項: 時間切れで未完了の旅客の扱い、FixedRuleの具体的な定義（公平な比較になるよう要検討）

## 作業ルール
- シーンはGUIで手作りしない。`AirportSim > Build Airport Scene`（`Assets/_Project/Editor/AirportSceneBuilder.cs`）で生成し、レイアウト変更はこのスクリプトを直す
- パラメータは `SimConfig`（ScriptableObject）に出す。コードにマジックナンバーを埋めない
- 乱数は `Random.InitState(seed)` で固定し、再現できるようにする
- Unity を開いたままだと別プロセスで同じプロジェクトを開けない。コンパイル確認はエディタのコンソールを見るか、エディタを閉じて `-batchmode` で行う
- `.meta` ファイルは必ずコミットする。`Library/` `Temp/` `Logs/` `UserSettings/` は除外済み
- コミットメッセージは日本語でも英語でもよいが、1コミット1目的にする

## ディレクトリ
- `Assets/_Project/Scripts/{Core,Environment,Agents,MetaAI,Metrics}` 本体コード
- `Assets/_Project/Editor` シーン生成などのEditor拡張
- `Assets/_Project/Settings` SimConfig アセット
- `analysis/` Python 集計・作図、`docs/` 設計メモ
