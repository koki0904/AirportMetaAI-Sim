using UnityEngine;
using UnityEngine.AI;

namespace AirportSim
{
    /// <summary>案内ロボット。Policy と Meta-AI の戦術に応じて行動する（v0 はルールベース）。</summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class GuideRobot : MonoBehaviour
    {
        SimulationManager sim;
        NavMeshAgent nav;
        Vector3 home;
        Vector3 lastDestination = new Vector3(float.MaxValue, 0f, 0f);
        PassengerAgent target;

        public void Init(SimulationManager s, Vector3 homePosition)
        {
            sim = s;
            home = homePosition;
            nav = GetComponent<NavMeshAgent>();
        }

        void Update()
        {
            if (sim == null || nav == null || !nav.isOnNavMesh) return;

            switch (sim.Config.policy)
            {
                case PolicyMode.FixedRule:
                    // 固定ルール: 持ち場から動かず、近くの旅客を案内するだけ（TODO: 公平な比較になるよう定義を見直す）
                    MoveTo(home);
                    GuideNearby(Time.deltaTime);
                    break;

                case PolicyMode.MetaAI:
                    RunMetaAI();
                    break;
            }
        }

        void RunMetaAI()
        {
            switch (sim.Meta.Current)
            {
                case Tactic.FatigueFirstAssistance:
                    RunFatigueFirst();
                    break;
                case Tactic.QueueEqualization:
                    // 保安検査場の手前で案内する（レーン選択の偏り是正は SimulationManager.ChooseZone 側）
                    Zone sec = sim.FirstZone(ZoneType.Security);
                    MoveTo(sec != null ? sec.transform.position + new Vector3(-sec.size.x, 0f, 0f) : home);
                    GuideNearby(Time.deltaTime);
                    break;
                default:
                    MoveTo(home);
                    GuideNearby(Time.deltaTime);
                    break;
            }
        }

        void RunFatigueFirst()
        {
            if (target == null || target.IsDone || target.IsAssisted)
                target = sim.FindNeediestPassenger(transform.position);

            if (target == null)
            {
                MoveTo(home);
                return;
            }

            MoveTo(target.transform.position);
            if ((target.transform.position - transform.position).magnitude <= sim.Config.assistRadius)
            {
                target.Assist(sim.Config.assistDuration);
                target = null;
            }
        }

        void GuideNearby(float dt)
        {
            float r2 = sim.Config.guidanceRadius * sim.Config.guidanceRadius;
            foreach (PassengerAgent p in sim.Passengers)
            {
                if (p == null || p.IsDone) continue;
                if ((p.transform.position - transform.position).sqrMagnitude <= r2) p.ReceiveGuidance(dt);
            }
        }

        void MoveTo(Vector3 dest)
        {
            if ((dest - lastDestination).sqrMagnitude < 0.25f) return;
            lastDestination = dest;
            nav.SetDestination(dest);
        }
    }
}
