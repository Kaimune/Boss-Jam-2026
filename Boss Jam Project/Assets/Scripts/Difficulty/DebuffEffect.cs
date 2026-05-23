using UnityEngine;

namespace BossJam.Difficulty
{
    /// <summary>
    /// Base class for all debuff effects. Each concrete subclass is its own
    /// ScriptableObject — author one asset per effect on disk and drop it
    /// into a DebuffEntry's effects list.
    ///
    /// Why SO instead of [SerializeReference] inline classes: the inspector
    /// UX for SerializeReference lists is rough (null elements with no
    /// obvious type picker). SOs render as plain object references, so the
    /// inspector dropdown / drag-and-drop just works. They're also reusable:
    /// the same "+15% boss speed" effect can be shared between two tiers
    /// without duplicating data.
    /// </summary>
    public abstract class DebuffEffect : ScriptableObject
    {
        /// <summary>Applied once when the parent DebuffEntry lands.</summary>
        public abstract void Apply(DifficultyRuntime runtime);
    }

    /// <summary>
    /// Numeric stat tweak. Pushes one row onto the runtime's modifier ledger;
    /// consumers see the new effective value on their next Get() call.
    /// </summary>
    [CreateAssetMenu(menuName = "BossJam/Difficulty/Stat Modifier Effect",
                     fileName = "StatModifierEffect")]
    public sealed class StatModifierEffect : DebuffEffect
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
