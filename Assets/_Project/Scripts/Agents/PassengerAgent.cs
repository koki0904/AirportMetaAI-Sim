using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace AirportSim
{
    public enum PassengerState { Walking, Queued, Serving, Done }

    /// <summary>旅客エージェント。ルート上のゾーンを順に回り、各ゾーンの待ち行列に並ぶ。</summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class PassengerAgent : MonoBehaviour
    {
        public int Id { get; private set; }
        public HumanState State { get; private set; }
        public PassengerState Phase { get; private set; }
        public float SpawnTime { get; private set; }
        public float FinishTime { get; private set; }
        public float TotalWait { get; private set; }
        public float AssistedUntil { get; private set; }

        public bool IsDone { get { return Phase == PassengerState.Done; } }
        public bool IsAssisted { get { return Time.time < AssistedUntil; } }

        SimulationManager sim;
        NavMeshAgent nav;
        Renderer rend;
        List<ZoneType> route;
        int routeIndex = -1;
        Zone target;
        float queuedAt;

        public void Init(SimulationManager s, int id, List<ZoneType> r, HumanState st)
        {
            sim = s;
            Id = id;
            route = r;
            State = st;
            SpawnTime = Time.time;
            nav = GetComponent<NavMeshAgent>();
            rend = GetComponent<Renderer>();
            GoToNext();
        }

        void Update()
        {
            if (IsDone) return;

            float density = (Phase == PassengerState.Queued && target != null) ? target.Density : 0f;
            State.Tick(Time.deltaTime, Phase, density, IsAssisted, sim.Config);

            if (Phase == PassengerState.Walking)
            {
                float mult = State.SpeedMultiplier;
                if (IsAssisted) mult = Mathf.Max(mult, sim.Config.assistedSpeedMultiplier);
                nav.speed = sim.Config.walkSpeed * mult;

                if (target != null && !nav.pathPending && nav.remainingDistance <= nav.stoppingDistance + sim.Config.arriveTolerance)
                {
                    Phase = PassengerState.Queued;
                    queuedAt = Time.time;
                    target.Enqueue(this);
                }
            }

            // 可視化: ストレスが高いほど赤くする
            rend.material.color = Color.Lerp(Color.white, new Color(0.9f, 0.2f, 0.2f), State.stress);
        }

        // ---- Zone / Robot から呼ばれる ----

        public void OnServiceStart()
        {
            TotalWait += Time.time - queuedAt;
            Phase = PassengerState.Serving;
        }

        public void OnServiceComplete()
        {
            GoToNext();
        }

        public void Assist(float duration)
        {
            AssistedUntil = Mathf.Max(AssistedUntil, Time.time + duration);
        }

        public void ReceiveGuidance(float dt)
        {
            State.stress = Mathf.Clamp01(State.stress - sim.Config.guidanceStressReliefPerSecond * dt);
        }

        // ---- 内部 ----

        void GoToNext()
        {
            routeIndex++;
            if (routeIndex >= route.Count)
            {
                Phase = PassengerState.Done;
                FinishTime = Time.time;
                sim.FinishPassenger(this);
                return;
            }
            target = sim.ChooseZone(route[routeIndex], this);
            Phase = PassengerState.Walking;
            nav.isStopped = false;
            nav.SetDestination(target.RandomQueuePoint());
        }
    }
}
