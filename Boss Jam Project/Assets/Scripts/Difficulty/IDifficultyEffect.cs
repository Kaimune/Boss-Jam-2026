using System;
using UnityEngine;

namespace BossJam.Difficulty
{
    /// <summary>
    /// Inline (non-SO) effect attached to a Difficulty tier. Implementations
    /// are stored via [SerializeReference] on Difficulty.effects, so they
    /// serialize as inline polymorphic data with no per-effect asset on disk.
    ///
    /// Each Apply() runs every time the runtime advances into a tier — the
    /// runtime clears its ledger + flag bag first, so effects can assume a
    /// blank slate.
    /// </summary>
    public interface IDifficultyEffect
    {
        void Apply(DifficultyRuntime runtime);
    }

    /// <summary>
    /// Convenience base class. [Serializable] is needed for SerializeReference
    /// to surface the type in Unity's inspector dropdown. Concrete effects
    /// don't have to extend this — implementing IDifficultyEffect on any
    /// [Serializable] plain class works — but extending this keeps the
    /// declaration uniform.
    /// </summary>
    [Serializable]
    public abstract class DifficultyEffectBase : IDifficultyEffect
    {
        public abstract void Apply(DifficultyRuntime runtime);
    }
}
