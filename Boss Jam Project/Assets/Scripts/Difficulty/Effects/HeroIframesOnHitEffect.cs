using System;
using UnityEngine;

namespace BossJam.Difficulty
{
    /// <summary>
    /// Grants the hero a window of invulnerability after taking damage.
    /// HeroEnemy.TakeDamage reads runtime.Flags.HeroPostHitIframeSeconds and
    /// calls SetInvulnFor when nonzero.
    /// </summary>
    [Serializable]
    public sealed class HeroIframesOnHitEffect : DifficultyEffectBase
    {
        [Min(0f)]
        public float seconds = 1f;

        public override void Apply(DifficultyRuntime runtime)
        {
            runtime.Flags.HeroPostHitIframeSeconds = seconds;
        }
    }
}
