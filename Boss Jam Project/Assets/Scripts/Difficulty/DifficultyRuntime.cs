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

        [Tooltip("Persists wave/applied debuffs across scene reloads. The runtime " +
                 "rebuilds its modifier ledger from this on Awake.")]
        [SerializeField] private RunState runState;

        public DifficultyProfile Profile => profile;
        public RunState State => runState;

        // ── Modifier ledger (rebuilt on Awake from runState.appliedEntries) ──
        private readonly List<Modifier> applied = new();

        public int AppliedCount => runState != null ? runState.appliedEntries.Count : 0;
        public IReadOnlyList<DebuffEntry> Applied =>
            runState != null ? runState.appliedEntries : System.Array.Empty<DebuffEntry>();
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
        public event Action<DebuffEntry> TierChanged;

        // Default tier name shown before any debuff has been applied.
        // Anything asking the runtime for the tier prior to the first hero
        // kill sees this string.
        public const string ImmortalTierName = "Immortal";

        public string CurrentTierName =>
            runState != null ? runState.currentTierName : ImmortalTierName;
        public DebuffEntry CurrentTierEntry =>
            runState != null ? runState.currentTierEntry : null;

        // ── Cutscene-facing surface ──────────────────────────────────
        /// <summary>1-based wave counter. Starts at 1, increments inside ApplyNextDebuff.</summary>
        public int CurrentWaveIndex =>
            runState != null ? runState.currentWaveIndex : 1;

        /// <summary>Tier label shown on the tier card during the hero-death cutscene.</summary>
        public string NextTierLabel => $"TIER {CurrentWaveIndex + 1}";

        /// <summary>One-line description of the debuff that's about to be applied. "(final wave)" when none queued.</summary>
        public string NextDebuffDescription
        {
            get
            {
                if (!HasNextDebuff) return "(final wave)";
                return PeekNextDebuffDescription();
            }
        }

        private string PeekNextDebuffDescription()
        {
            var entry = NextPreview;
            if (entry == null) return "(final wave)";
            if (!string.IsNullOrWhiteSpace(entry.description)) return entry.description;
            if (!string.IsNullOrWhiteSpace(entry.tierDescription)) return entry.tierDescription;
            if (!string.IsNullOrWhiteSpace(entry.name)) return entry.name;
            return "(unnamed debuff)";
        }

        // ── Wire-up ──────────────────────────────────────────────────
        private void Awake()
        {
            if (runState == null)
            {
                Debug.LogWarning(
                    $"{nameof(DifficultyRuntime)}: no RunState assigned — wave/debuff state will not persist.",
                    this);
                return;
            }

            // Rebuild the modifier ledger from persisted debuffs. The scene
            // was just reloaded, so `applied` is empty; replay each entry's
            // effect so consumers see the correct effective values.
            for (int i = 0; i < runState.appliedEntries.Count; i++)
            {
                runState.appliedEntries[i]?.effect?.Apply(this);
            }
        }

        private void OnEnable()
        {
            HeroEnemy.HeroKilledStatic += OnHeroKilled;
        }

        private void OnDisable()
        {
            HeroEnemy.HeroKilledStatic -= OnHeroKilled;
        }

        // ── Trigger ──────────────────────────────────────────────────
        // Hero death just raises the event now — debuff application is
        // gated by ApplyNextDebuff() so the game can pause + show a
        // next-tier preview screen first.
        private void OnHeroKilled() => HeroKilled?.Invoke();

        /// <summary>
        /// Commit the next debuff from the profile. Called by
        /// GameStateController.Resume() after the player presses Space on the
        /// tier-advance screen. No-op if the curve is exhausted.
        /// </summary>
        public void ApplyNextDebuff()
        {
            if (profile == null || runState == null) return;
            if (AppliedCount >= profile.debuffs.Count) return;

            var entry = profile.debuffs[AppliedCount];
            runState.appliedEntries.Add(entry);
            entry.effect?.Apply(this);

            // Promote tier if this entry names a new one. Blank tierName
            // inherits the previous label (lets multiple debuffs share a tier).
            bool tierPromoted = false;
            if (!string.IsNullOrEmpty(entry.tierName) && entry.tierName != runState.currentTierName)
            {
                runState.currentTierName = entry.tierName;
                runState.currentTierEntry = entry;
                tierPromoted = true;
            }

            DebuffApplied?.Invoke(entry);
            Debug.Log($"[Difficulty] Applied #{AppliedCount} '{entry.name}' — {entry.description}");

            if (tierPromoted) TierChanged?.Invoke(runState.currentTierEntry);

            runState.currentWaveIndex++;
        }

        public bool HasNextDebuff =>
            profile != null && AppliedCount < profile.debuffs.Count;

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
