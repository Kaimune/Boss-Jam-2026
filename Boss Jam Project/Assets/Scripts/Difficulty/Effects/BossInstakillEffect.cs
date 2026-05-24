using System;

namespace BossJam.Difficulty
{
    [Serializable]
    public sealed class BossInstakillEffect : DifficultyEffectBase
    {
        public override void Apply(DifficultyRuntime runtime)
        {
            runtime.Flags.BossInstakill = true;
        }
    }
}
