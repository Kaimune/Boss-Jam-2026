using System;
using UnityEngine;

namespace BossJam.Difficulty
{
    [Serializable]
    public sealed class FireballHomingEffect : DifficultyEffectBase
    {
        [Min(0f), Tooltip("Maximum turning rate while seeking the boss. Higher = sharper homing.")]
        public float turnRateDegPerSec = 180f;

        public override void Apply(DifficultyRuntime runtime)
        {
            ref var f = ref runtime.Flags;
            f.FireballHoming = true;
            f.FireballTurnRateDegPerSec = turnRateDegPerSec;
        }
    }
}
