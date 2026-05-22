using System;
using System.Collections.Generic;
using BossJam.Attacks;
using BossJam.Enemies;
using BossJam.GridSystem;
using BossJam.Player;
using UnityEngine;

namespace BossJam.Difficulty
{
    /// <summary>
    /// Per-scene hub for the difficulty system. Owns the applied debuff
    /// ledger, the shared flag bag, and the lifecycle events that effects
    /// hook into. Consumers (HeroEnemy, BossController, IAttack scripts)
    /// query Get / GetInt against this runtime to read effective stat values.
    ///
    /// Baseline assets are never mutated — Get reads the supplied baseValue
    /// and folds the ledger over it. With an empty profile the runtime is
    /// inert and the game plays exactly as it would without it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DifficultyRuntime : MonoBehaviour
    {
        [SerializeField] private DifficultyProfile profile;

        public DifficultyProfile Profile => profile;

        // ── Applied ledger ───────────────────────────────────────────
        private readonly List<Modifier> applied = new();
        private readonly List<DebuffEntry> appliedEntries = new();

        public int AppliedCount => appliedEntries.Count;
        public IReadOnlyList<DebuffEntry> Applied => appliedEntries;
        public DebuffEntry NextPreview =>
            (profile != null && AppliedCount < profile.debuffs.Count)
                ? profile.debuffs[AppliedCount]
                : null;

        // ── Actor handles ────────────────────────────────────────────
        // Set by consumers in their Awake/OnEnable. May be null between
        // hero deaths or before the boss spawns.
        public BossController Boss { get; set; }
        public HeroEnemy Hero { get; set; }

        // ── Flag bag ─────────────────────────────────────────────────
        // Populated as content demands. Effects mutate via `rt.Flags.X = …`;
        // consumers read via `rt.Flags.X` inline at the relevant call site.
        [Serializable]
        public struct DifficultyFlags
        {
            // (no flags yet — add fields here as behavioral debuffs land)
        }

        private DifficultyFlags flags;
        public ref DifficultyFlags Flags => ref flags;

        // ── Lifecycle events ─────────────────────────────────────────
        // Re-broadcast so effects only depend on the runtime, not on the
        // static event source.
        public event Action HeroKilled;
        public event Action<int, IGridEntity> BossDamaged;
        public event Action<AttackConfig> AttackStarted;
        public event Action<AttackConfig, IDamageable> AttackHit;

        // UI-facing events
        public event Action<DebuffEntry> DebuffApplied;
        public event Action<string> TierChanged;

        public string CurrentTierName { get; private set; } = "";

        // ── Wire-up ──────────────────────────────────────────────────
        private void OnEnable()
        {
            HeroEnemy.HeroKilledStatic += OnHeroKilled;
        }

        private void OnDisable()
        {
            HeroEnemy.HeroKilledStatic -= OnHeroKilled;
        }

        // ── Trigger ──────────────────────────────────────────────────
        private void OnHeroKilled()
        {
            HeroKilled?.Invoke();

            if (profile == null) return;
            if (AppliedCount >= profile.debuffs.Count) return;

            var entry = profile.debuffs[AppliedCount];
            appliedEntries.Add(entry);
            entry.effect?.Apply(this);

            DebuffApplied?.Invoke(entry);
            Debug.Log($"[Difficulty] Applied #{AppliedCount} '{entry.name}' — {entry.description}");

            RecomputeTier();
        }

        // ── Effect-side helpers ──────────────────────────────────────
        public void AddModifier(Target target, Op op, float value,
                                string attackId = null, string extensionKey = null)
        {
            applied.Add(new Modifier
            {
                target = target,
                op = op,
                value = value,
                attackId = attackId ?? "",
                extensionKey = extensionKey ?? "",
            });
        }

        // Plumbing used by BossController / attack hitboxes to feed events
        // through the runtime so effects can subscribe to them generically.
        public void RaiseBossDamaged(int amount, IGridEntity source)
            => BossDamaged?.Invoke(amount, source);

        public void RaiseAttackStarted(AttackConfig cfg)
            => AttackStarted?.Invoke(cfg);

        public void RaiseAttackHit(AttackConfig cfg, IDamageable target)
            => AttackHit?.Invoke(cfg, target);

        // ── Consumer-side queries ────────────────────────────────────
        public float Get(Target target, float baseValue,
                         string attackId = null, string extensionKey = null)
        {
            float adds = 0f;
            float muls = 1f;
            for (int i = 0; i < applied.Count; i++)
            {
                var m = applied[i];
                if (m.target != target) continue;
                if (m.attackId.Length > 0 && m.attackId != attackId) continue;
                if (m.extensionKey.Length > 0 && m.extensionKey != extensionKey) continue;

                if (m.op == Op.Mul) muls *= m.value;
                else adds += m.value;
            }
            return (baseValue + adds) * muls;
        }

        public int GetInt(Target target, int baseValue,
                          string attackId = null, string extensionKey = null)
            => Mathf.RoundToInt(Get(target, baseValue, attackId, extensionKey));

        // ── Internals ────────────────────────────────────────────────
        private void RecomputeTier()
        {
            string newName = "";
            if (profile != null)
            {
                for (int i = 0; i < profile.tiers.Count; i++)
                {
                    var tier = profile.tiers[i];
                    if (AppliedCount >= tier.startsAtDebuffCount) newName = tier.name;
                }
            }
            if (newName != CurrentTierName)
            {
                CurrentTierName = newName;
                TierChanged?.Invoke(newName);
            }
        }

        private struct Modifier
        {
            public Target target;
            public Op op;
            public float value;
            public string attackId;
            public string extensionKey;
        }
    }
}
