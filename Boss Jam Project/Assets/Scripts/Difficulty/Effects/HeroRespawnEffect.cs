using System;
using UnityEngine;

namespace BossJam.Difficulty
{
    [Serializable]
    public sealed class HeroRespawnEffect : DifficultyEffectBase
    {
        public HeroRespawnMode mode = HeroRespawnMode.None;

        [Min(0)] public int thresholdHp = 1;

        [Min(0.05f)] public float windowSeconds = 1.5f;

        [Min(0)] public int restoreHp = 3;

        public bool replayIntro = false;

        public override void Apply(DifficultyRuntime runtime)
        {
            ref var f = ref runtime.Flags;
            f.HeroRespawnMode = mode;
            f.HeroRespawnThresholdHp = thresholdHp;
            f.HeroRespawnWindowSeconds = windowSeconds;
            f.HeroRespawnRestoreHp = restoreHp;
            f.HeroRespawnReplayIntro = replayIntro;
        }
    }
}
