using UnityEngine;

namespace AirportSim
{
    public enum Tactic { None = 0, QueueEqualization = 1, FatigueFirstAssistance = 2 }

    /// <summary>Meta-AI v0。一定間隔で空港全体を観測し、戦術を1つ選ぶ（ルールベース。強化学習は次段階）。</summary>
    public class MetaAIController : MonoBehaviour
    {
        SimulationManager sim;
        float nextDecisionTime;

        public Tactic Current { get; private set; }

        public void Init(SimulationManager s) { sim = s; }

        void Update()
        {
            if (sim == null || sim.Config.policy != PolicyMode.MetaAI) return;
            if (Time.time < nextDecisionTime) return;

            nextDecisionTime = Time.time + sim.Config.metaAIIntervalSeconds;
            Current = Decide();
            sim.Kpi.LogTactic(Current);
        }

        Tactic Decide()
        {
            // 観測: レーン間の人数差、支援が必要な旅客数
            if (sim.MaxLaneImbalance() >= sim.Config.imbalanceThreshold) return Tactic.QueueEqualization;
            if (sim.CountAssistCandidates() >= sim.Config.fatigueCountThreshold) return Tactic.FatigueFirstAssistance;
            return Tactic.None;
        }
    }
}
