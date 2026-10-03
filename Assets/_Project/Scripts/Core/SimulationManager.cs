using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

namespace AirportSim
{
    /// <summary>シミュレーション全体の進行管理。旅客とロボットの生成、レーン選択、終了処理を担う。</summary>
    public class SimulationManager : MonoBehaviour
    {
        public SimConfig configAsset;
        public Transform[] robotHomes;

        public SimConfig Config { get; private set; }
        public KpiLogger Kpi { get; private set; }
        public MetaAIController Meta { get; private set; }
        public readonly List<PassengerAgent> Passengers = new List<PassengerAgent>();
        public readonly List<Zone> Zones = new List<Zone>();
        public int SpawnedCount { get { return nextId; } }

        readonly Dictionary<ZoneType, List<Zone>> zonesByType = new Dictionary<ZoneType, List<Zone>>();
        readonly List<GuideRobot> robots = new List<GuideRobot>();
        float spawnAccumulator;
        int nextId;
        bool finished;

        /// <summary>FatigueFirstAssistance 発動中は、支援中の旅客を待ち行列で優先する。</summary>
        public bool PriorityActive
        {
            get { return Config.policy == PolicyMode.MetaAI && Meta != null && Meta.Current == Tactic.FatigueFirstAssistance; }
        }

        void Awake()
        {
            Config = Instantiate(configAsset);
            ApplyCommandLine(Config);
            Time.timeScale = Config.timeScale;
            Random.InitState(Config.seed);

            Zones.AddRange(FindObjectsByType<Zone>(FindObjectsSortMode.None));
            Zones.Sort((a, b) => string.CompareOrdinal(a.name, b.name)); // 乱数の再現性のため順序を固定
            foreach (Zone z in Zones)
            {
                z.Bind(this);
                if (!zonesByType.ContainsKey(z.type)) zonesByType[z.type] = new List<Zone>();
                zonesByType[z.type].Add(z);
            }

            Kpi = GetComponent<KpiLogger>();
            Meta = GetComponent<MetaAIController>();
            EnsureNavMesh();
        }

        void Start()
        {
            Kpi.Init(this);
            Meta.Init(this);
            SpawnRobots();
        }

        void Update()
        {
            if (finished) return;

            if (Time.time < Config.spawnUntilSeconds)
            {
                float perMinute = Config.spawnRatePerMinute;
                if (Config.scenario == ScenarioType.Congested) perMinute *= Config.congestedMultiplier;
                spawnAccumulator += Time.deltaTime * perMinute / 60f;
                while (spawnAccumulator >= 1f)
                {
                    spawnAccumulator -= 1f;
                    SpawnPassenger();
                }
            }

            if (Time.time >= Config.durationSeconds) Finish();
        }

        // ---- 旅客 ----

        void SpawnPassenger()
        {
            Zone entrance = FirstZone(ZoneType.Entrance);
            if (entrance == null) return;

            HumanState st = new HumanState();
            bool vulnerable = Random.value < Config.highFatigueShare;
            st.fatigue = vulnerable ? Random.Range(Config.highFatigueThreshold, 0.9f) : Random.Range(0f, 0.3f);
            st.initialFatigue = st.fatigue;
            st.stress = Random.Range(0f, 0.2f);
            st.timeSlack = Random.value;

            List<ZoneType> route = new List<ZoneType> { ZoneType.CheckIn, ZoneType.Security };
            if (st.timeSlack >= Config.shoppingSlackThreshold) route.Add(ZoneType.Shopping);
            route.Add(ZoneType.Gate);

            GameObject go = CreateAgentBody("Passenger_" + nextId, entrance.RandomPoint(), 0.8f, Color.white);
            if (go == null) return;
            PassengerAgent p = go.AddComponent<PassengerAgent>();
            p.Init(this, nextId, route, st);
            nextId++;
            Passengers.Add(p);
        }

        public void FinishPassenger(PassengerAgent p)
        {
            Kpi.RecordPassenger(p);
            Passengers.Remove(p);
            Destroy(p.gameObject);
        }

        // ---- ロボット ----

        void SpawnRobots()
        {
            if (Config.policy == PolicyMode.NoRobot || robotHomes == null || robotHomes.Length == 0) return;
            for (int i = 0; i < Config.robotCount; i++)
            {
                Transform home = robotHomes[i % robotHomes.Length];
                GameObject go = CreateAgentBody("Robot_" + i, home.position, 1.0f, new Color(0.2f, 0.5f, 1f));
                if (go == null) continue;
                GuideRobot r = go.AddComponent<GuideRobot>();
                r.Init(this, home.position);
                robots.Add(r);
            }
        }

        // ---- レーン選択・問い合わせ ----

        public Zone FirstZone(ZoneType type)
        {
            List<Zone> list;
            return zonesByType.TryGetValue(type, out list) && list.Count > 0 ? list[0] : null;
        }

        /// <summary>旅客が次に向かうゾーンを選ぶ。並列レーンがある場合、QueueEqualization 発動中は最短レーン、それ以外はランダム。</summary>
        public Zone ChooseZone(ZoneType type, PassengerAgent p)
        {
            List<Zone> list = zonesByType[type];
            if (list.Count == 1) return list[0];

            bool equalize = Config.policy == PolicyMode.MetaAI && Meta != null && Meta.Current == Tactic.QueueEqualization;
            if (equalize)
            {
                Zone best = list[0];
                foreach (Zone z in list) if (z.Occupants < best.Occupants) best = z;
                return best;
            }
            return list[Random.Range(0, list.Count)];
        }

        /// <summary>並列レーンを持つゾーン種別ごとの最大人数差（Meta-AI の観測値）。</summary>
        public int MaxLaneImbalance()
        {
            int maxDiff = 0;
            foreach (KeyValuePair<ZoneType, List<Zone>> kv in zonesByType)
            {
                if (kv.Value.Count < 2) continue;
                int lo = int.MaxValue, hi = int.MinValue;
                foreach (Zone z in kv.Value) { lo = Mathf.Min(lo, z.Occupants); hi = Mathf.Max(hi, z.Occupants); }
                maxDiff = Mathf.Max(maxDiff, hi - lo);
            }
            return maxDiff;
        }

        /// <summary>支援が必要な（高疲労かつ未支援の）旅客数。</summary>
        public int CountAssistCandidates()
        {
            int n = 0;
            foreach (PassengerAgent p in Passengers)
                if (p != null && !p.IsDone && !p.IsAssisted && p.State.fatigue >= Config.highFatigueThreshold) n++;
            return n;
        }

        public PassengerAgent FindNeediestPassenger(Vector3 from)
        {
            PassengerAgent best = null;
            float bestD = float.MaxValue;
            foreach (PassengerAgent p in Passengers)
            {
                if (p == null || p.IsDone || p.IsAssisted) continue;
                if (p.State.fatigue < Config.highFatigueThreshold) continue;
                float d = (p.transform.position - from).sqrMagnitude;
                if (d < bestD) { best = p; bestD = d; }
            }
            return best;
        }

        // ---- 内部 ----

        GameObject CreateAgentBody(string objName, Vector3 pos, float scale, Color color)
        {
            NavMeshHit hit;
            if (!NavMesh.SamplePosition(pos, out hit, 3f, NavMesh.AllAreas))
            {
                Debug.LogError("NavMesh が見つかりません。AirportSim > Build Airport Scene を実行してください。");
                return null;
            }
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = objName;
            go.transform.localScale = Vector3.one * scale;
            go.transform.position = hit.position;
            Collider col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            go.GetComponent<Renderer>().material.color = color;

            NavMeshAgent nav = go.AddComponent<NavMeshAgent>();
            nav.radius = 0.3f * scale / 0.8f;
            nav.height = 2f * scale;
            nav.baseOffset = scale; // カプセルの中心が床から浮くように
            nav.speed = Config.walkSpeed;
            nav.acceleration = 8f;
            return go;
        }

        void EnsureNavMesh()
        {
            if (NavMesh.CalculateTriangulation().vertices.Length > 0) return;
            foreach (NavMeshSurface s in FindObjectsByType<NavMeshSurface>(FindObjectsSortMode.None)) s.BuildNavMesh();
        }

        void Finish()
        {
            finished = true;
            Kpi.WriteResults();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        static void ApplyCommandLine(SimConfig c)
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                string key = args[i];
                string val = args[i + 1];
                if (key == "-policy")
                {
                    PolicyMode pm;
                    if (System.Enum.TryParse(val, true, out pm)) c.policy = pm;
                }
                else if (key == "-scenario")
                {
                    ScenarioType sc;
                    if (System.Enum.TryParse(val, true, out sc)) c.scenario = sc;
                }
                else if (key == "-seed")
                {
                    int s;
                    if (int.TryParse(val, out s)) c.seed = s;
                }
                else if (key == "-duration")
                {
                    float d;
                    if (float.TryParse(val, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out d)) c.durationSeconds = d;
                }
                else if (key == "-timescale")
                {
                    float t;
                    if (float.TryParse(val, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out t)) c.timeScale = t;
                }
            }
        }
    }
}
