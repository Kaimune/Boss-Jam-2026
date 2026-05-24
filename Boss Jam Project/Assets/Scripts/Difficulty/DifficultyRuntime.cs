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
    /// Per-scene hub for the difficulty system. Owns the modifier ledger,
    /// the shared flag bag, and lifecycle events. Consumers (HeroEnemy,
    /// BossController, IAttack scripts) query Get / GetInt against this
    /// runtime to read effective stat values.
    ///
    /// Tier semantics: each tier in the profile defines ABSOLUTE state.
    /// AdvanceTier() clears the modifier ledger AND the flag bag before
    /// applying the next tier's effects.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    public sealed class DifficultyRuntime : MonoBehaviour
    {
        [SerializeField] private DifficultyProfile profile;

        [Tooltip("Persists wave / applied tier list across scene reloads. The runtime " +
                 "rebuilds its modifier ledger from this on Awake.")]
        [SerializeField] private RunState runState;

        public DifficultyProfile Profile => profile;
        public RunState State => runState;

        // ── Modifier ledger (rebuilt on Awake by replaying applied tier effects) ──
        private readonly List<Modifier> applied = new();

        public int AppliedCount => runState != null ? runState.appliedTiers.Count : 0;
        public IReadOnlyList<Difficulty> Applied =>
            runState != null ? runState.appliedTiers : System.Array.Empty<Difficulty>();
        public Difficulty NextPreview =>
            (profile != null && AppliedCount < profile.tiers.Count)
                ? profile.tiers[AppliedCount]
                : null;

        // ── Actor handles ────────────────────────────────────────────
        public BossController Boss { get; set; }
        public HeroEnemy Hero { get; set; }

        // ── Flag bag ─────────────────────────────────────────────────
        // Cleared on AdvanceTier(); populated by IDifficultyEffect.Apply.
        [Serializable]
        public struct DifficultyFlags
        {
            // Boss
            public bool BossInstakill;

            // Hero — regen
            public int   HeroRegenHpPerInterval;
            public float HeroRegenIntervalSeconds;

            // Hero — iframes after taking damage
            public float HeroPostHitIframeSeconds;

            // Hero — respawn rules
            public HeroRespawnMode HeroRespawnMode;
            public int   HeroRespawnThresholdHp;
            public float HeroRespawnWindowSeconds;
            public int   HeroRespawnRestoreHp;
            public bool  HeroRespawnReplayIntro;

            // Fireball
            public float FireballSpeedMultiplier;
            public bool  FireballHoming;
            public float FireballTurnRateDegPerSec;
        }

        private DifficultyFlags flags;
        public ref DifficultyFlags Flags => ref flags;

        // ── Lifecycle events ─────────────────────────────────────────
        public event Action HeroKilled;
        public event Action<int, IGridEntity> BossDamaged;
        public event Action<AttackConfig> AttackStarted;
        public event Action<AttackConfig, IDamageable> AttackHit;

        // UI-facing events
        public event Action<Difficulty> TierApplied;
        public event Action<Difficulty> TierChanged;

        public string CurrentTierName =>
            runState != null ? runState.currentTierName : "";
        public Difficulty CurrentTierEntry =>
            runState != null ? runState.currentTierEntry : null;

        public string PreviousTierName =>
            runState != null ? runState.previousTierName : "";
        public Difficulty PreviousTierEntry =>
            runState != null ? runState.previousTierEntry : null;

        public Difficulty LastAppliedEntry
        {
            get
            {
                if (runState == null) return null;
                int n = runState.appliedTiers.Count;
                return n > 0 ? runState.appliedTiers[n - 1] : null;
            }
        }

        // ── Cutscene-facing surface ──────────────────────────────────
        public int CurrentWaveIndex =>
            runState != null ? runState.currentWaveIndex : 1;

        public string NextTierLabel => $"TIER {CurrentWaveIndex + 1}";

        public string NextDebuffDescription
        {
            get
            {
                if (!HasNextTier) return "(final wave)";
                return PeekNextDebuffDescription();
            }
        }

        private string PeekNextDebuffDescription()
        {
            var entry = NextPreview;
            if (entry == null) return "(final wave)";
            if (!string.IsNullOrWhiteSpace(entry.description)) return entry.description;
            if (!string.IsNullOrWhiteSpace(entry.tierDescription)) return entry.tierDescription;
            if (!string.IsNullOrWhiteSpace(entry.tierName)) return entry.tierName;
            return "(unnamed tier)";
        }

        // ── Wire-up ──────────────────────────────────────────────────
        private void Awake()
        {
            ResetFlagsToDefaults();

            if (runState == null)
            {
                Debug.LogWarning(
                    $"{nameof(DifficultyRuntime)}: no RunState assigned — wave/tier state will not persist.",
                    this);
                return;
            }

            // Debug starting tier — honored once per playmode session. If set, we
            // pre-populate appliedTiers to put the runtime at that tier; subsequent
            // rehydration replays only the latest applied tier's effects (reset-on-
            // advance). The debug field itself survives ResetForNewRun by design, so
            // the user's choice persists across playmode entries until they clear it.
            if (profile != null
                && runState.appliedTiers.Count == 0
                && runState.debugStartingTier > 0)
            {
                int target = Mathf.Min(runState.debugStartingTier, profile.tiers.Count);
                for (int i = 0; i < target; i++)
                {
                    var tier = profile.tiers[i];
                    if (tier == null) continue;
                    runState.appliedTiers.Add(tier);
                    if (!string.IsNullOrEmpty(tier.tierName))
                    {
                        runState.currentTierName = tier.tierName;
                        runState.currentTierEntry = tier;
                    }
                    runState.currentWaveIndex++;
                }
            }

            // Mid-run rehydration: replay the most recent tier's effects so the
            // ledger / flag bag reflect the current tier (reset-on-advance means
            // only the LATEST tier defines current state).
            if (runState.appliedTiers.Count > 0)
            {
                var current = runState.appliedTiers[runState.appliedTiers.Count - 1];
                ApplyAllEffects(current);
            }
        }

        private void ResetFlagsToDefaults()
        {
            flags = default;
            flags.FireballSpeedMultiplier = 1f;
            flags.HeroRespawnMode = HeroRespawnMode.None;
        }

        private void ApplyAllEffects(Difficulty entry)
        {
            if (entry == null || entry.effects == null) return;
            for (int i = 0; i < entry.effects.Count; i++)
                entry.effects[i]?.Apply(this);
        }

        private void OnEnable()
        {
            HeroEnemy.HeroKilledStatic += OnHeroKilled;
        }

        private void OnDisable()
        {
            HeroEnemy.HeroKilledStatic -= OnHeroKilled;
        }

        private void OnHeroKilled() => HeroKilled?.Invoke();

        /// <summary>
        /// Commit the next tier. Clears the modifier ledger and flag bag,
        /// then applies the new tier's effects. No-op if the curve is exhausted.
        /// </summary>
        public void AdvanceTier()
        {
            if (profile == null || runState == null) return;
            if (AppliedCount >= profile.tiers.Count) return;

            var entry = profile.tiers[AppliedCount];
            if (entry == null) return;

            // Snapshot pre-application tier for transition animations.
            runState.previousTierName = runState.currentTierName;
            runState.previousTierEntry = runState.currentTierEntry;

            // Reset before applying: every tier defines absolute state.
            applied.Clear();
            ResetFlagsToDefaults();

            runState.appliedTiers.Add(entry);
            runState.pendingTierNarration = !string.IsNullOrWhiteSpace(entry.narrationScriptName);
            ApplyAllEffects(entry);

            bool tierPromoted = false;
            if (!string.IsNullOrEmpty(entry.tierName) && entry.tierName != runState.currentTierName)
            {
                runState.currentTierName = entry.tierName;
                runState.currentTierEntry = entry;
                tierPromoted = true;
            }

            TierApplied?.Invoke(entry);
            Debug.Log($"[Difficulty] Advanced to #{AppliedCount} '{entry.tierName}' — {entry.description}");

            if (tierPromoted) TierChanged?.Invoke(runState.currentTierEntry);

            runState.currentWaveIndex++;
        }

        public bool HasNextTier =>
            profile != null && AppliedCount < profile.tiers.Count;

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
            bool hasOverride = false;
            float overrideValue = 0f;
            for (int i = 0; i < applied.Count; i++)
            {
                var m = applied[i];
                if (m.target != target) continue;
                if (m.attackId.Length > 0 && m.attackId != attackId) continue;
                if (m.extensionKey.Length > 0 && m.extensionKey != extensionKey) continue;

                switch (m.op)
                {
                    case Op.Mul: muls *= m.value; break;
                    case Op.Add: adds += m.value; break;
                    case Op.Override: hasOverride = true; overrideValue = m.value; break;
                }
            }
            if (hasOverride) return overrideValue;
            return (baseValue + adds) * muls;
        }

        public int GetInt(Target target, int baseValue,
                          string attackId = null, string extensionKey = null)
            => Mathf.RoundToInt(Get(target, baseValue, attackId, extensionKey));

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
