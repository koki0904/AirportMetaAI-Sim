using System.Collections.Generic;
using UnityEngine;

namespace AirportSim
{
    public enum ZoneType { Entrance, CheckIn, Security, Shopping, Gate }

    /// <summary>空港のゾーン。待ち行列と処理窓口（servers 個、1人あたり serviceTime 秒）を持つ。</summary>
    public class Zone : MonoBehaviour
    {
        public ZoneType type;
        public Vector2 size = new Vector2(8f, 8f); // x, z 方向の広さ（m）
        public int servers = 2;
        public float serviceTime = 10f;

        class Slot
        {
            public PassengerAgent passenger;
            public float endTime;
        }

        readonly List<PassengerAgent> queue = new List<PassengerAgent>();
        readonly List<Slot> serving = new List<Slot>();
        SimulationManager sim;

        public int QueueLength { get { return queue.Count; } }
        public int Occupants { get { return queue.Count + serving.Count; } }
        public float AreaM2 { get { return Mathf.Max(1f, size.x * size.y); } }
        /// <summary>人口密度（人/㎡）。待ち行列と処理中の人数を、ゾーン面積で割った値。</summary>
        public float Density { get { return Occupants / AreaM2; } }

        public void Bind(SimulationManager s) { sim = s; }

        public Vector3 RandomPoint()
        {
            return transform.position + new Vector3(
                Random.Range(-size.x * 0.5f, size.x * 0.5f), 0f,
                Random.Range(-size.y * 0.5f, size.y * 0.5f));
        }

        /// <summary>待ち行列の位置（ゾーンの手前側＝x の小さい側）。重ならないようランダムに散らす。</summary>
        public Vector3 RandomQueuePoint()
        {
            return transform.position + new Vector3(
                Random.Range(-size.x * 0.5f, 0f), 0f,
                Random.Range(-size.y * 0.5f, size.y * 0.5f));
        }

        public void Enqueue(PassengerAgent p) { queue.Add(p); }

        void Update()
        {
            if (sim == null) return;

            for (int i = serving.Count - 1; i >= 0; i--)
            {
                if (Time.time >= serving[i].endTime)
                {
                    PassengerAgent p = serving[i].passenger;
                    serving.RemoveAt(i);
                    p.OnServiceComplete();
                }
            }

            while (serving.Count < servers && queue.Count > 0)
            {
                PassengerAgent next = PickNext();
                queue.Remove(next);
                Slot slot = new Slot();
                slot.passenger = next;
                slot.endTime = Time.time + serviceTime * Random.Range(0.8f, 1.2f);
                serving.Add(slot);
                next.OnServiceStart();
            }
        }

        PassengerAgent PickNext()
        {
            if (sim.PriorityActive)
            {
                foreach (PassengerAgent p in queue) if (p.IsAssisted) return p;
            }
            return queue[0];
        }
    }
}
