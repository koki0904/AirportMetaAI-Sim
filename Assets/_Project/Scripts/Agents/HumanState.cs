using UnityEngine;

namespace AirportSim
{
    /// <summary>旅客の状態。いずれも 0〜1。疲労は歩行で増え、ストレスは待ち行列の混雑で増える。</summary>
    [System.Serializable]
    public class HumanState
    {
        public float fatigue;
        public float initialFatigue;
        public float stress;
        public float timeSlack;

        public float SpeedMultiplier { get { return Mathf.Lerp(1f, 0.5f, Mathf.Clamp01(fatigue)); } }

        public void Tick(float dt, PassengerState phase, float localDensity, bool assisted, SimConfig c)
        {
            if (phase == PassengerState.Walking)
                fatigue += c.fatigueRatePerSecond * dt * (assisted ? 0.3f : 1f);

            if (phase == PassengerState.Queued)
                stress += c.stressRatePerSecond * dt * (1f + localDensity * c.stressDensityFactor) * (assisted ? 0.3f : 1f);
            else
                stress -= c.stressRecoveryPerSecond * dt;

            if (assisted)
                fatigue -= c.assistFatigueRecoveryPerSecond * dt;

            fatigue = Mathf.Clamp01(fatigue);
            stress = Mathf.Clamp01(stress);
        }
    }
}
