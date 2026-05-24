using System;
using UnityEngine;

namespace BossJam.Difficulty
{
    /// <summary>
    /// Numeric stat tweak — the inline-effect replacement for the old
    /// StatModifierEffect ScriptableObject. Pushes one row onto the runtime's
    /// modifier ledger; consumers see the new effective value on their next
    /// Get()/GetInt() call.
    /// </summary>
    [Serializable]
    public sealed class StatModifierEffect : DifficultyEffectBase
    {
        public Target target;
        public Op op = Op.Mul;
        public float value = 1f;

        [Tooltip("Empty = applies to every attack. Set to a specific AttackConfig.id (e.g. 'charge_slam') to scope.")]
        public string attackId;

        [Tooltip("Only used when target == AttackExtension. Per-attack stat key (e.g. 'dashDistance').")]
        public string extensionKey;

        public override void Apply(DifficultyRuntime runtime)
            => runtime.AddModifier(target, op, value, attackId, extensionKey);
    }
}
