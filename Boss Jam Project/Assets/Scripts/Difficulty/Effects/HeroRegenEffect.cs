using System;
using UnityEngine;

namespace BossJam.Difficulty
{
    /// <summary>
    /// Configures hero HP regen for the current tier. Writes into the runtime
    /// flag bag; HeroEnemy ticks regen each frame using these values.
    /// </summary>
    [Serializable]
    public sealed class HeroRegenEffect : DifficultyEffectBase
    {
        [Min(0)]
        public int hpPerInterval = 1;

        [Min(0.1f)]
        public float intervalSeconds = 5f;

        public override void Apply(DifficultyRuntime runtime)
        {
            ref var f = ref runtime.Flags;
            f.HeroRegenHpPerInterval = hpPerInterval;
            f.HeroRegenIntervalSeconds = intervalSeconds;
        }
    }
}
