using System;
using System.Collections.Generic;
using UnityEngine;

namespace BossJam.Difficulty
{
    /// <summary>
    /// Polymorphic effect attached to a <see cref="DebuffEntry"/>. Called once
    /// when the debuff is applied (i.e. when its hero kill lands). The effect
    /// is the only place that bridges authored data and the runtime's three
    /// surfaces: the stat modifier ledger, the flag bag, and the lifecycle
    /// event hooks.
    ///
    /// Implementations are plain [Serializable] C# classes so they live
    /// inline inside the DifficultyProfile asset via [SerializeReference],
    /// not as separate SO files.
    /// </summary>
    public interface IDebuffEffect
    {
        void Apply(DifficultyRuntime rt);
    }

    /// <summary>
    /// Numeric stat tweak. Pushes one row onto the runtime's modifier ledger;
    /// consumers see the new effective value on their next Get() call.
    /// </summary>
    [Serializable]
    public sealed class StatModifierEffect : IDebuffEffect
    {
        public Target target;
        public Op op = Op.Mul;
        public float value = 1f;

        [Tooltip("Empty = applies to every attack. Set to a specific AttackConfig.id (e.g. 'charge_slam') to scope.")]
        public string attackId;

        [Tooltip("Only used when target == AttackExtension. Per-attack stat key (e.g. 'dashDistance').")]
        public string extensionKey;

        public void Apply(DifficultyRuntime rt)
            => rt.AddModifier(target, op, value, attackId, extensionKey);
    }

    /// <summary>
    /// Bundles multiple effects under one DebuffEntry slot so a single hero
    /// kill can trigger several stat changes or behaviors at once. Nested
    /// effects are stored via [SerializeReference] so the Inspector dropdown
    /// works the same as the top-level effect field.
    /// </summary>
    [Serializable]
    public sealed class CompositeEffect : IDebuffEffect
    {
        [SerializeReference] public List<IDebuffEffect> effects = new();

        public void Apply(DifficultyRuntime rt)
        {
            if (effects == null) return;
            for (int i = 0; i < effects.Count; i++) effects[i]?.Apply(rt);
        }
    }
}
