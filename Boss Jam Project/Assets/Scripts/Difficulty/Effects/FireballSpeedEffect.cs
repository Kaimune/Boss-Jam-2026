using System;
using UnityEngine;

namespace BossJam.Difficulty
{
    [Serializable]
    public sealed class FireballSpeedEffect : DifficultyEffectBase
    {
        [Min(0.01f)]
        public float multiplier = 1f;

        public override void Apply(DifficultyRuntime runtime)
        {
            runtime.Flags.FireballSpeedMultiplier = multiplier;
        }
    }
}
