using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace AirportSim
{
    /// <summary>KPI の記録と CSV 出力。KPI の定義は CLAUDE.md を参照（勝手に変えない）。</summary>
    public class KpiLogger : MonoBehaviour
    {
        SimulationManager sim;
        float sampleTimer;
        float peakDensity;
        readonly StringBuilder rows = new StringBuilder();
        int completed;
        double sumWait;
        int highFatigueCount;
        double highFatigueSumWait;
        readonly int[] tacticCounts = new int[3];

        public void Init(SimulationManager s)
        {
            sim = s;
            rows.AppendLine("id,spawnTime,finishTime,totalTime,waitTime,initialFatigue,finalStress,highFatigue");
        }

        void Update()
        {
            if (sim == null) return;
            sampleTimer += Time.deltaTime;
            if (sampleTimer < 1f) return;
            sampleTimer = 0f;

            foreach (Zone z in sim.Zones)
            {
                if (z.type != ZoneType.CheckIn && z.type != ZoneType.Security) continue;
                if (z.Density > peakDensity) peakDensity = z.Density;
            }
        }

        public void RecordPassenger(PassengerAgent p)
        {
            bool high = p.State.initialFatigue >= sim.Config.highFatigueThreshold;
            completed++;
            sumWait += p.TotalWait;
            if (high)
            {
                highFatigueCount++;
                highFatigueSumWait += p.TotalWait;
            }
            rows.AppendLine(string.Join(",", new string[]
            {
                p.Id.ToString(), F(p.SpawnTime), F(p.FinishTime), F(p.FinishTime - p.SpawnTime), F(p.TotalWait),
                F(p.State.initialFatigue), F(p.State.stress), high ? "1" : "0"
            }));
        }

        public void LogTactic(Tactic t) { tacticCounts[(int)t]++; }

        public void WriteResults()
        {
            SimConfig c = sim.Config;
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", c.outputDir));
            Directory.CreateDirectory(dir);
            string tag = c.policy + "_" + c.scenario + "_" + c.seed;
            File.WriteAllText(Path.Combine(dir, "passengers_" + tag + ".csv"), rows.ToString());

            string summaryPath = Path.Combine(dir, "summary.csv");
            StringBuilder sb = new StringBuilder();
            if (!File.Exists(summaryPath))
                sb.AppendLine("policy,scenario,seed,spawned,completed,avgWait,peakDensity,avgWaitHighFatigue,tacticNone,tacticQueueEq,tacticFatigueFirst");

            string avgWait = completed > 0 ? F(sumWait / completed) : "";
            string avgHigh = highFatigueCount > 0 ? F(highFatigueSumWait / highFatigueCount) : "";
            sb.AppendLine(string.Join(",", new string[]
            {
                c.policy.ToString(), c.scenario.ToString(), c.seed.ToString(),
                sim.SpawnedCount.ToString(), completed.ToString(), avgWait, F(peakDensity), avgHigh,
                tacticCounts[0].ToString(), tacticCounts[1].ToString(), tacticCounts[2].ToString()
            }));
            File.AppendAllText(summaryPath, sb.ToString());
            Debug.Log("KPI written: " + dir);
        }

        static string F(double v) { return v.ToString("F3", CultureInfo.InvariantCulture); }
    }
}
