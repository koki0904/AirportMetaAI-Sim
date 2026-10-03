using UnityEngine;

namespace AirportSim
{
    public enum PolicyMode { NoRobot, FixedRule, MetaAI }
    public enum ScenarioType { Normal, Congested }

    /// <summary>実験パラメータ。値は仮置き。コードにマジックナンバーを埋めず、ここで調整する。</summary>
    [CreateAssetMenu(menuName = "AirportSim/SimConfig", fileName = "SimConfig")]
    public class SimConfig : ScriptableObject
    {
        [Header("実験条件（コマンドラインで上書き可: -policy -scenario -seed -duration -timescale）")]
        public PolicyMode policy = PolicyMode.FixedRule;
        public ScenarioType scenario = ScenarioType.Normal;
        public int seed = 0;
        public float durationSeconds = 600f;      // シミュレーション内の秒
        public float spawnUntilSeconds = 480f;    // この時刻以降は旅客を発生させない
        public float timeScale = 1f;

        [Header("旅客発生")]
        public float spawnRatePerMinute = 20f;
        public float congestedMultiplier = 1.3f;  // Congested シナリオでの発生率倍率
        public float highFatigueShare = 0.2f;     // 高疲労で開始する旅客の割合
        public float shoppingSlackThreshold = 0.6f; // timeSlack がこれ以上なら Shopping に寄る

        [Header("移動")]
        public float walkSpeed = 1.4f;
        public float arriveTolerance = 1.5f;

        [Header("Human State")]
        public float highFatigueThreshold = 0.6f;
        public float fatigueRatePerSecond = 0.0015f;
        public float stressRatePerSecond = 0.004f;
        public float stressDensityFactor = 2f;
        public float stressRecoveryPerSecond = 0.001f;

        [Header("ロボット")]
        public int robotCount = 2;
        public float guidanceRadius = 4f;               // 近くの旅客のストレスを下げる範囲
        public float guidanceStressReliefPerSecond = 0.003f;
        public float assistRadius = 2.5f;               // 疲労優先支援を開始する距離
        public float assistDuration = 60f;
        public float assistedSpeedMultiplier = 0.9f;
        public float assistFatigueRecoveryPerSecond = 0.002f;

        [Header("Meta-AI v0（ルールベース）")]
        public float metaAIIntervalSeconds = 10f;
        public int imbalanceThreshold = 4;       // レーン間の人数差がこれ以上 → QueueEqualization
        public int fatigueCountThreshold = 2;    // 支援候補がこの人数以上 → FatigueFirstAssistance

        [Header("出力")]
        public string outputDir = "Results";
    }
}
