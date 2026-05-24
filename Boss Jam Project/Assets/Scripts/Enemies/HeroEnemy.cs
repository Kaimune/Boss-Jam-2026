using System.Collections.Generic;
using BossJam.Difficulty;
using BossJam.GridSystem;
using BossJam.Player;
using UnityEngine;

namespace BossJam.Enemies
{
    /// <summary>
    /// AI-driven hero. Kites the boss at a preferred distance while strafing,
    /// and periodically lobs fireballs at it. Takes damage from boss hitboxes.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(GridFootprint))]
    [RequireComponent(typeof(GridMover))]
    public class HeroEnemy : MonoBehaviour, IGridEntity, IDamageable, IInvulnerable
    {
        [Header("Refs")]
        [SerializeField] private Transform target;
        [SerializeField] private BossGrid grid;
        [Tooltip("Tunable hero stats. If unassigned, falls back to a runtime-created default with the values defined in HeroConfig.cs.")]
        [SerializeField] private HeroConfig config;

        [Header("Abilities")]
        [SerializeField] private HeroBrain brain;
        [SerializeField] private HeroMelee melee;
        [SerializeField] private HeroFireball fireball;
        [SerializeField] private HeroDodge dodge;

        // Public read-only views the ability components query each frame.
        public HeroConfig Config => config;
        public BossGrid Grid => grid;

        private int currentHp;
        private int spawnedMaxHp;

        public int MaxHp => spawnedMaxHp;
        public int CurrentHp => currentHp;
        public event System.Action<int, int> HpChanged;

        // Static so subscribers (e.g. DifficultyRuntime) survive hero respawns.
        public static event System.Action HeroKilledStatic;

        private DifficultyRuntime rt;
        private float Eff(Target t, float b)  => rt != null ? rt.Get(t, b)    : b;
        private int   EffI(Target t, int b)   => rt != null ? rt.GetInt(t, b) : b;

        [Header("Kiting (runtime-only)")]
        [Tooltip("+1 rotates the off-grid search CCW around the boss, -1 CW. Flipped when stuck.")]
        [SerializeField] private int orbitSign = 1;

        [Header("Facing")]
        [Tooltip("Transform that rotates to face movement. Leave null to rotate the root.")]
        [SerializeField] private Transform visual;
        [SerializeField, Min(0f)] private float turnDegreesPerSecond = 720f;
        [Tooltip("Yaw offset (degrees) applied to the visual. Spin this until the model's nose points along movement.")]
        [SerializeField, Range(-180f, 180f)] private float modelYawOffset = 0f;
        private Quaternion facingTarget = Quaternion.identity;

        [Header("Debug")]
        [SerializeField] private bool drawKiteVisual = true;
        [SerializeField] private Color kiteVisualColor = new Color(0.2f, 1f, 0.6f, 0.9f);
        [SerializeField, Min(0.01f)] private float kiteLineWidth = 0.08f;
        [SerializeField, Min(0.05f)] private float kiteDotScale = 0.35f;
        [Tooltip("World-space Y lift so the visual draws above the ground plane.")]
        [SerializeField, Min(0f)] private float kiteVisualYLift = 0.1f;

        private GridFootprint cachedFootprint;
        public GridFootprint Footprint =>
            cachedFootprint != null ? cachedFootprint : (cachedFootprint = GetComponent<GridFootprint>());

        public float TickMultiplier => Eff(Target.HeroTickMultiplier, config.tickMultiplier);
        public Team Team => Team.Hero;

        public Verdict OnEnteredBy(IGridEntity mover)
        {
            if (mover == null) return Verdict.Block;

            // Other heroes pass through allies (future-proof; single-hero today).
            if (mover.Team == Team.Hero) return Verdict.Pass;

            // Our own fireballs (Team.Enemy) pass through — required so a
            // self-spawned Fireball can register in our spawn cell.
            if (mover.Team == Team.Enemy) return Verdict.Pass;

            // Boss attack hitboxes are Team.Boss + IDamageDealer → pass, damage us.
            if (mover.Team == Team.Boss && mover is IDamageDealer dd)
                return Verdict.PassWith(() => TakeDamage(dd.Damage, mover));

            // Boss body, neutrals → block. Boss must attack to clear us.
            return Verdict.Block;
        }

        public void TakeDamage(int amount, IGridEntity source)
        {
            if (IsInvulnerable) return;
            currentHp = Mathf.Max(0, currentHp - amount);
            Debug.Log($"HeroEnemy '{name}' took {amount} damage (hp={currentHp})");
            HpChanged?.Invoke(currentHp, spawnedMaxHp);
            if (currentHp <= 0)
            {
                if (TryConsumeRespawnOnLethal(amount))
                {
                    HpChanged?.Invoke(currentHp, spawnedMaxHp);
                    return;
                }
                HeroKilledStatic?.Invoke();
                Destroy(gameObject);
                return;
            }
            BeginHitReaction();

            // Tier-driven post-hit iframes. BeginHitReaction already grants iframes
            // tied to the combo/knockback window; this stacks an extra window for
            // tiers (e.g. Easy) that grant generous invulnerability between hits.
            float iframeSec = rt != null ? rt.Flags.HeroPostHitIframeSeconds : 0f;
            if (iframeSec > 0f) SetInvulnFor(iframeSec);
        }

        // ── Hit reaction ─────────────────────────────────────────────
        // Two-phase model so the player can combo:
        //   T=0       hit lands. Hero stunned, NO iframes, NO push yet.
        //   T=combo   big knockback fires + iframes start. The combo window
        //             between T=0 and T=combo is when chained player hits
        //             can stack damage.
        [Header("Hit reaction")]
        [Tooltip("Combo window after a hit. Hero is stunned but vulnerable — chained player attacks landing in this window all do damage. When it lapses, the big knockback fires.")]
        [SerializeField, Min(0f)] private float comboWindowSeconds = 0.5f;
        [Tooltip("Cells the hero teleports along the knockback direction when the combo window ends. The shove is instant, not a per-frame push.")]
        [SerializeField, Min(0)] private int knockbackDistanceCells = 6;
        [Tooltip("Iframe + stun window that begins when the knockback fires (after the combo window). The brain resumes once this lapses.")]
        [SerializeField, Min(0f)] private float iframesAfterKnockbackSeconds = 0.5f;

        private float invulnUntil = -1f;
        private float stunUntil = -1f;
        private float knockbackFiresAt = -1f;

        public bool IsInvulnerable => Time.time < invulnUntil;
        public bool IsStunned => Time.time < stunUntil;

        public void SetInvulnFor(float seconds)
        {
            if (seconds <= 0f) return;
            var until = Time.time + seconds;
            if (until > invulnUntil) invulnUntil = until;
        }

        private bool TryConsumeRespawnOnLethal(int incomingDamage)
        {
            if (rt == null) return false;
            ref var f = ref rt.Flags;
            switch (f.HeroRespawnMode)
            {
                // Tier 6 only. Tier 7+ switches the flag to FullHpIfNotInstakilled,
                // so this branch is unreachable from tier 7 onward.
                case HeroRespawnMode.SaveScumOnFirstOneHp:
                    if (saveScumConsumed) return false;
                    saveScumConsumed = true;

                    // Save-scum can permanently raise the max for this life —
                    // the lore is "you reloaded a better save", and the design
                    // intent is e.g. 5 hp on a tier where the base cap is 3.
                    int restored = Mathf.Max(1, f.HeroRespawnRestoreHp);
                    if (restored > spawnedMaxHp) spawnedMaxHp = restored;
                    currentHp = restored;
                    HpChanged?.Invoke(currentHp, spawnedMaxHp);

                    TeleportToSpawn();
                    SetInvulnFor(0.5f);
                    {
                        int tier = rt.AppliedCount;
                        BossJam.Game.GameStateController.Instance?.PlayInGameDialogue($"respawn_wave_{tier}");
                    }
                    return true;

                case HeroRespawnMode.FullHpIfNotInstakilled:
                    if (conditionalRespawnArmedAt > 0f && Time.time <= conditionalRespawnArmedAt + f.HeroRespawnWindowSeconds)
                    {
                        if (incomingDamage < spawnedMaxHp)
                        {
                            currentHp = spawnedMaxHp;
                            SetInvulnFor(0.5f);
                            conditionalRespawnArmedAt = -1f;
                            return true;
                        }
                    }
                    return false;

                default:
                    return false;
            }
        }

        private void BeginHitReaction()
        {
            // Hero plants in place for the combo window — no iframes — so
            // chained player attacks can stack damage. FireDelayedKnockback
            // teleports the hero away and flips on iframes when the window ends.
            float now = Time.time;
            knockbackFiresAt = now + comboWindowSeconds;
            stunUntil = now + comboWindowSeconds + iframesAfterKnockbackSeconds;

            // Abort any in-progress ability so the brain isn't mid-dodge or
            // mid-fireball-spawn when control resumes. Brain.CancelAll calls
            // Cancel() on each registered ability.
            if (brain != null) brain.CancelAll();
        }

        // Instant displacement. Tries the full knockback distance first, then
        // walks the target closer one cell at a time until GridFootprint
        // accepts (walls, other actors). No-op if even a one-cell shove is
        // blocked.
        private void FireDelayedKnockback()
        {
            knockbackFiresAt = -1f;
            SetInvulnFor(iframesAfterKnockbackSeconds);

            if (mover == null || Footprint == null || knockbackDistanceCells <= 0) return;
            Vector2 dir = ComputeKnockbackDirection();
            Vector2 origin = Footprint.Anchor;
            for (int d = knockbackDistanceCells; d > 0; d--)
            {
                Vector2 target = origin + dir * d;
                if (mover.DriveTo(target)) return;
            }
        }

        private Vector2 ComputeKnockbackDirection()
        {
            Vector2 away = Vector2.zero;
            if (bossRef != null && bossRef.Footprint != null && Footprint != null)
            {
                Vector2 myCenter   = Footprint.Anchor + Footprint.Footprint * 0.5f;
                Vector2 bossCenter = bossRef.Footprint.Anchor + bossRef.Footprint.Footprint * 0.5f;
                away = myCenter - bossCenter;
            }
            if (away.sqrMagnitude < 0.0001f)
            {
                // Hero is dead on top of boss (rare). Fall back to the kite
                // direction, or up if even that's zero.
                return Vector2.up;
            }
            // Snap to cardinal so the push lands on a grid-aligned cell.
            return Mathf.Abs(away.x) > Mathf.Abs(away.y)
                ? new Vector2(Mathf.Sign(away.x), 0f)
                : new Vector2(0f, Mathf.Sign(away.y));
        }

        // Moves the hero (and its grid footprint) back to the spawn location.
        // Used by save-scum respawn — UX intent is "reload at scene start".
        // GridFootprint owns the cell-space anchor; transform position is the
        // world renderer position. Drive both so the grid system stays
        // consistent with the visual.
        private void TeleportToSpawn()
        {
            transform.position = spawnWorldPosition;
            var fp = Footprint;
            if (fp != null)
            {
                // Hard-set the anchor (bypass collision check) — the spawn
                // cell is known-safe at scene start, and the hero already
                // cleared the grid via the lethal hit that triggered this.
                fp.Configure(spawnFootprintAnchor, fp.Footprint, fp.Grid);
            }
            // Brake any in-flight movement input so the hero plants for a
            // frame before the brain re-engages.
            if (mover != null) mover.InputDirection = Vector2.zero;

            // Clear any in-flight hit-reaction state so the post-respawn
            // hero is fully controllable.
            knockbackFiresAt = -1f;
            stunUntil = -1f;
        }

        private GridMover mover;
        private GridFootprint targetFootprint;
        private float stuckTimer;

        // Tier-driven HP regen. Flags are populated by HeroRegenEffect.Apply; the
        // ticker advances during Playing state only and is suppressed at max hp.
        private float regenAccumulator;

        // Respawn state — SaveScumOnFirstOneHp consumes once per tier (reset on
        // TierApplied). FullHpIfNotInstakilled timer is armed each time hp falls
        // below threshold and cleared when restored.
        private static bool saveScumConsumed;
        private float conditionalRespawnArmedAt = -1f;

        // Spawn position captured at Awake — the post-intro spawn point. Used by
        // save-scum to teleport the hero back to this exact spot on respawn.
        private Vector3 spawnWorldPosition;
        private Vector2 spawnFootprintAnchor;
        private Vector2 kiteTarget;
        private bool hasKiteTarget;
        private BossPredictor predictor;
        private Vector2 predictedBossCenter;
        private BossJam.Player.BossController bossRef;
        private bool lastDodgeActive;
        private bool tickAppliedAsBoosted;

        // Runtime debug visual (visible in Game view without needing Gizmos toggle).
        private LineRenderer kiteLine;
        private Transform kiteDot;
        private Material kiteLineMaterial;
        private Material kiteDotMaterial;

        private void Awake()
        {
            EnsureConfig();
            rt = FindFirstObjectByType<DifficultyRuntime>();
            if (rt != null)
            {
                rt.Hero = this;
                rt.TierApplied -= OnTierApplied;
                rt.TierApplied += OnTierApplied;
            }

            // HP is snapshotted at spawn: debuffs that later raise/lower HeroMaxHp
            // affect the NEXT hero, not this living one.
            spawnedMaxHp = EffI(Target.HeroMaxHp, config.maxHp);
            currentHp = spawnedMaxHp;

            mover = GetComponent<GridMover>();
            if (grid == null && Footprint != null) grid = Footprint.Grid;
            if (visual == null) visual = transform;

            // Capture the scene-authored spawn pose BEFORE the brain ever
            // moves us — Awake runs before the first Update, so this is the
            // post-intro spawn point. Save-scum teleports back to this.
            spawnWorldPosition = transform.position;
            spawnFootprintAnchor = Footprint != null ? Footprint.Anchor : Vector2.zero;
            // Honour the scene-authored idle rotation as the starting facing.
            facingTarget = visual.rotation;

            // When spawned from a prefab the scene-bound target ref is null —
            // resolve to the boss in the scene.
            bossRef = FindFirstObjectByType<BossJam.Player.BossController>();
            if (target == null && bossRef != null) target = bossRef.transform;

            // Auto-resolve ability components if the inspector didn't wire them.
            if (brain == null)    brain    = GetComponent<HeroBrain>();
            if (melee == null)    melee    = GetComponent<HeroMelee>();
            if (fireball == null) fireball = GetComponent<HeroFireball>();
            if (dodge == null)    dodge    = GetComponent<HeroDodge>();

            // Each ability self-determines whether it's available this wave
            // via its IsEnabled property: an `enabledByDefault` toggle on the
            // ability script, optionally overridden by a DifficultyRuntime
            // modifier on the matching Target.*Enabled enum entry. The brain
            // only sees abilities that resolved to enabled.
            if (brain != null)
            {
                if (melee != null && melee.IsEnabled)      brain.RegisterAbility(melee);
                if (fireball != null && fireball.IsEnabled) brain.RegisterAbility(fireball);
                if (dodge != null && dodge.IsEnabled)      brain.RegisterAbility(dodge);

                // Quiet the component so its internal state (cooldown timers,
                // OnEnable side effects) doesn't tick during waves where it's
                // disabled. Melee runs lean already so we leave it alone.
                if (fireball != null) fireball.enabled = fireball.IsEnabled;
                if (dodge != null)    dodge.enabled    = dodge.IsEnabled;
            }

            // Predictor tuning is read-once at spawn. Reaction/velocity-window
            // debuffs apply to heroes spawned after the debuff lands; the existing
            // predictor's stored values aren't reactively updated.
            float react   = Eff(Target.HeroReactionTimeSeconds,   config.reactionTimeSeconds);
            float velWin  = Eff(Target.HeroVelocityWindowSeconds, config.velocityWindowSeconds);
            float buffer  = Mathf.Max(config.perceptionBufferSeconds, react + velWin + 0.1f);
            predictor = new BossPredictor(buffer)
            {
                ReactionTimeSeconds   = react,
                VelocityWindowSeconds = velWin,
                MaxExtrapolationCells = config.maxExtrapolationCells,
            };
        }

        private void OnDestroy()
        {
            if (rt != null && rt.Hero == this) rt.Hero = null;
            if (rt != null) rt.TierApplied -= OnTierApplied;
            if (kiteLineMaterial != null) Destroy(kiteLineMaterial);
            if (kiteDotMaterial != null) Destroy(kiteDotMaterial);
        }

        private void OnTierApplied(BossJam.Difficulty.Difficulty d) => saveScumConsumed = false;

        private void OnEnable() => ApplyTick();
        private void OnValidate() => ApplyTick();

        // The HeroConfig field can be left unassigned in the Inspector during early dev;
        // we synthesize a default in-memory instance at play time so the hero still
        // runs with the class-default values. Skipped in edit mode so OnValidate
        // doesn't spam warnings or leak SO instances every time the prefab is opened.
        private void EnsureConfig()
        {
            if (config != null) return;
            if (!Application.isPlaying) return;
            config = ScriptableObject.CreateInstance<HeroConfig>();
            config.hideFlags = HideFlags.HideAndDontSave;
            Debug.LogWarning($"HeroEnemy '{name}' has no HeroConfig assigned; using runtime defaults.", this);
        }

        private void ApplyTick() => ApplyTickInternal(boosted: false);

        // boosted == true folds the dodge speed multiplier into the GridMover
        // tick so the hero physically moves faster during a dodge window.
        private void ApplyTickInternal(bool boosted)
        {
            EnsureConfig();
            if (config == null) return; // edit mode + unassigned: nothing to scale yet
            float tickMul = Eff(Target.HeroTickMultiplier, config.tickMultiplier);
            foreach (var t in GetComponentsInChildren<ITickScalable>(includeInactive: true))
                t.ApplyTick(tickMul);

            // Movement uses an explicit cells/sec baseline (moveSpeed * moveSpeedMultiplier),
            // back-solved into the tick scale GridMover already consumes:
            //   CellsPerSecond = 1 / (TickDuration * tickScale)  ⇒  tickScale = 1 / (TickDuration * speed)
            var m = GetComponent<GridMover>();
            var g = grid != null ? grid : (Footprint != null ? Footprint.Grid : null);
            if (m != null && g != null)
            {
                // Folded through DifficultyRuntime so debuffs on HeroMoveSpeed
                // can scale / override the hero's cells/sec per wave.
                float baseline = config.moveSpeed * Mathf.Max(0.01f, config.moveSpeedMultiplier);
                float speed = Eff(Target.HeroMoveSpeed, baseline);
                if (boosted)
                {
                    float boost = Eff(Target.HeroDodgeSpeedMultiplier, config.dodgeSpeedMultiplier);
                    speed *= Mathf.Max(1f, boost);
                }
                if (speed > 0.0001f && g.TickDuration > 0.0001f)
                    m.ApplyTick(1f / (g.TickDuration * speed));
            }
        }

        private void Start()
        {
            // Awake-order between root GameObjects is undefined; resolve in Start.
            if (target != null) targetFootprint = target.GetComponent<GridFootprint>();
            HpChanged?.Invoke(currentHp, spawnedMaxHp);
        }

        private void Update()
        {
            if (BossJam.Game.GameStateController.Instance != null &&
                BossJam.Game.GameStateController.Instance.State != BossJam.Game.GameState.Playing)
                return;

            TickRegen(Time.deltaTime);
            TickConditionalRespawn();
            if (target == null) { mover.InputDirection = Vector2.zero; return; }

            // Fire the deferred knockback once the combo window lapses.
            if (knockbackFiresAt > 0f && Time.time >= knockbackFiresAt)
                FireDelayedKnockback();

            // Stun gate: brain ignored, kiting paused. The hero plants for the
            // whole combo window + post-knockback iframe period — the actual
            // shove is an instant teleport inside FireDelayedKnockback, not a
            // per-frame push.
            if (IsStunned)
            {
                mover.InputDirection = Vector2.zero;
                return;
            }

            UpdatePerception();

            // Compute kite first so the decision context can carry it. The
            // ability brain may then override the direction (dodge locks its
            // own vector at Begin time and replays it for the boost window).
            Vector2 kiteDir = ComputeSteering(bossInPunishWindowAndUnspent: PunishWindowOpenForApproach());
            var ctx = BuildContext(kiteDir);

            if (brain != null)
            {
                brain.TickAll(Time.deltaTime);
                var pick = brain.Choose(ctx);
                if (pick != null) brain.Commit(pick, ctx);
            }

            Vector2 inputDir = (dodge != null && dodge.IsActive) ? dodge.LockedDirection : kiteDir;
            // While an ability with LocksMovement is mid-animation (e.g. melee
            // swing window), hold the kite still so the hero plants for the clip.
            if (IsAbilityLockingMovement()) inputDir = Vector2.zero;
            mover.InputDirection = inputDir;
            ApplyFacing(inputDir);
            ApplyDodgeSpeedIfChanged();
            TickStuckDetector();
        }

        private bool IsAbilityLockingMovement()
        {
            if (melee != null && melee.IsBusy && melee.LocksMovement) return true;
            if (fireball != null && fireball.IsBusy && fireball.LocksMovement) return true;
            // Dodge intentionally never locks — it ACCELERATES movement.
            return false;
        }

        // Snap the facing target when there's a non-zero movement direction;
        // ease the visual rotation toward the target every frame. Easing runs
        // even when input is zero so the hero finishes turning after stopping.
        private void ApplyFacing(Vector2 dir)
        {
            if (visual == null) return;
            if (dir.sqrMagnitude > 0.0001f)
            {
                var forward = new Vector3(dir.x, 0f, dir.y);
                facingTarget = Quaternion.LookRotation(forward, Vector3.up)
                               * Quaternion.Euler(0f, modelYawOffset, 0f);
            }
            visual.rotation = Quaternion.RotateTowards(
                visual.rotation, facingTarget,
                turnDegreesPerSecond * Time.deltaTime);
        }

        // Hero approaches melee range when the boss has an opening AND the
        // brain hasn't already spent its hit this window.
        private bool PunishWindowOpenForApproach()
            => bossRef != null && bossRef.InPunishWindow
               && brain != null && !brain.PunishWindowConsumed;

        private HeroDecisionContext BuildContext(Vector2 kiteDir)
        {
            Vector2 myCenter = Footprint.Anchor + Footprint.Footprint * 0.5f;
            float dist = Vector2.Distance(myCenter, predictedBossCenter);
            bool bossExecuting = bossRef != null && bossRef.IsExecutingAttack;
            return new HeroDecisionContext
            {
                heroCenter         = myCenter,
                bossCenter         = predictedBossCenter,
                bossIsExecutingAttack = bossExecuting,
                kiteDir            = kiteDir,
                distanceToBossCells = dist,
                bossInPunishWindow = bossRef != null && bossRef.InPunishWindow,
            };
        }

        // Reapply mover tick when dodge state flips so the speed boost is
        // gated to exactly the dodge's active window. Avoids the per-frame
        // cost of recomputing the tick scale when nothing has changed.
        private void ApplyDodgeSpeedIfChanged()
        {
            bool dodging = dodge != null && dodge.IsActive;
            if (dodging == lastDodgeActive && dodging == tickAppliedAsBoosted) return;
            lastDodgeActive = dodging;
            tickAppliedAsBoosted = dodging;
            ApplyTickInternal(dodging);
        }

        private void UpdatePerception()
        {
            Vector2 realBossCenter = (targetFootprint != null)
                ? targetFootprint.Anchor + targetFootprint.Footprint * 0.5f
                : WorldToCellCenter(target.position);
            predictor.Observe(Time.time, realBossCenter);
            predictedBossCenter = predictor.Predict(Time.time, realBossCenter);
            RefreshHazardBuffer();
        }

        // Per-frame buffer of hazard rects the hero can perceive (reaction-lag
        // filtered). Reused list to avoid per-frame allocations.
        private readonly List<HeroKiteSteering.HazardRect> hazardBuffer = new();
        public IReadOnlyList<HeroKiteSteering.HazardRect> PerceivedHazards => hazardBuffer;

        private void RefreshHazardBuffer()
        {
            hazardBuffer.Clear();
            var all = Hazard.All;
            if (all == null || all.Count == 0) return;

            float now = Time.time;
            float reactLag = Eff(Target.HeroReactionTimeSeconds, config.reactionTimeSeconds);
            for (int i = 0; i < all.Count; i++)
            {
                var h = all[i];
                if (h == null) continue;
                // Hazards under the reaction-lag window are not yet perceived.
                if (now - h.BornAt < reactLag) continue;
                hazardBuffer.Add(new HeroKiteSteering.HazardRect
                {
                    Anchor    = h.Anchor,
                    Footprint = h.Footprint,
                });
            }
        }

        private Vector2 ComputeSteering(bool bossInPunishWindowAndUnspent)
        {
            Vector2 myCenter = Footprint.Anchor + Footprint.Footprint * 0.5f;

            // Dynamic preferred distance: collapse inward to melee range when
            // there's an opening to punish; otherwise kite at the full radius.
            float kiteDist = bossInPunishWindowAndUnspent
                ? Eff(Target.HeroMeleeApproachDistanceCells, config.meleeApproachDistanceCells)
                : Eff(Target.HeroPreferredDistanceCells, config.preferredDistanceCells);

            var r = HeroKiteSteering.Solve(
                myCenter, predictedBossCenter, kiteDist, grid,
                Footprint.Footprint, orbitSign,
                hazardBuffer);

            kiteTarget = r.TargetPoint;
            hasKiteTarget = r.ValidTargetFound;
            return r.Direction;
        }

        private void TickRegen(float dt)
        {
            if (rt == null) return;
            int amt = rt.Flags.HeroRegenHpPerInterval;
            float interval = rt.Flags.HeroRegenIntervalSeconds;
            if (amt <= 0 || interval <= 0f) { regenAccumulator = 0f; return; }
            if (currentHp >= spawnedMaxHp) { regenAccumulator = 0f; return; }

            regenAccumulator += dt;
            while (regenAccumulator >= interval && currentHp < spawnedMaxHp)
            {
                regenAccumulator -= interval;
                currentHp = Mathf.Min(spawnedMaxHp, currentHp + amt);
                HpChanged?.Invoke(currentHp, spawnedMaxHp);
            }
        }

        private void TickConditionalRespawn()
        {
            if (rt == null) return;
            ref var f = ref rt.Flags;
            if (f.HeroRespawnMode != HeroRespawnMode.FullHpIfNotInstakilled) return;

            if (currentHp <= f.HeroRespawnThresholdHp && conditionalRespawnArmedAt < 0f)
                conditionalRespawnArmedAt = Time.time;

            if (conditionalRespawnArmedAt > 0f && Time.time > conditionalRespawnArmedAt + f.HeroRespawnWindowSeconds)
            {
                if (currentHp > 0)
                {
                    currentHp = spawnedMaxHp;
                    HpChanged?.Invoke(currentHp, spawnedMaxHp);
                    {
                        int tier = rt.AppliedCount;
                        BossJam.Game.GameStateController.Instance?.PlayInGameDialogue($"respawn_wave_{tier}");
                    }
                }
                conditionalRespawnArmedAt = -1f;
            }

            if (conditionalRespawnArmedAt > 0f && currentHp > f.HeroRespawnThresholdHp)
                conditionalRespawnArmedAt = -1f;
        }

        private void TickStuckDetector()
        {
            // GridMover writes IsMoving=false when both axes were blocked this frame
            // (slide along walls still counts as moving). Wedged → flip orbit.
            if (mover.InputDirection != Vector2.zero && !mover.IsMoving)
                stuckTimer += Time.deltaTime;
            else
                stuckTimer = 0f;

            if (stuckTimer >= Eff(Target.HeroStuckFlipSeconds, config.stuckFlipSeconds))
            {
                orbitSign = -orbitSign;
                stuckTimer = 0f;
            }
        }

        private Vector2 WorldToCellCenter(Vector3 world)
        {
            var local = grid.transform.InverseTransformPoint(world);
            return new Vector2(local.x / grid.CellSize, local.z / grid.CellSize);
        }

        private void LateUpdate()
        {
            UpdateKiteVisual();
        }

        private void UpdateKiteVisual()
        {
            if (!drawKiteVisual || !hasKiteTarget || grid == null || Footprint == null)
            {
                if (kiteLine != null) kiteLine.enabled = false;
                if (kiteDot != null) kiteDot.gameObject.SetActive(false);
                return;
            }

            EnsureKiteVisual();
            Vector2 fp = Footprint.Footprint;
            Vector3 worldTarget = grid.FootprintCenterWorld(kiteTarget - fp * 0.5f, fp);
            Vector3 lift = Vector3.up * kiteVisualYLift;

            kiteLine.enabled = true;
            kiteLine.startWidth = kiteLineWidth;
            kiteLine.endWidth = kiteLineWidth;
            kiteLine.SetPosition(0, transform.position + lift);
            kiteLine.SetPosition(1, worldTarget + lift);

            kiteDot.gameObject.SetActive(true);
            kiteDot.position = worldTarget + lift;
            kiteDot.localScale = Vector3.one * (grid.CellSize * kiteDotScale);
        }

        private void EnsureKiteVisual()
        {
            if (kiteLine != null && kiteDot != null) return;

            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Sprites/Default");

            if (kiteLine == null)
            {
                var lineGo = new GameObject("HeroKiteLine") { hideFlags = HideFlags.HideAndDontSave };
                lineGo.transform.SetParent(transform, false);
                kiteLine = lineGo.AddComponent<LineRenderer>();
                kiteLine.useWorldSpace = true;
                kiteLine.positionCount = 2;
                kiteLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                kiteLine.receiveShadows = false;
                kiteLineMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                if (kiteLineMaterial.HasColor("_BaseColor")) kiteLineMaterial.SetColor("_BaseColor", kiteVisualColor);
                if (kiteLineMaterial.HasColor("_Color")) kiteLineMaterial.SetColor("_Color", kiteVisualColor);
                kiteLine.material = kiteLineMaterial;
                kiteLine.startColor = kiteVisualColor;
                kiteLine.endColor = kiteVisualColor;
            }

            if (kiteDot == null)
            {
                var dotGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                dotGo.name = "HeroKiteDot";
                dotGo.hideFlags = HideFlags.HideAndDontSave;
                var col = dotGo.GetComponent<Collider>();
                if (col != null) Destroy(col);
                dotGo.transform.SetParent(transform, worldPositionStays: false);
                kiteDot = dotGo.transform;

                var mr = dotGo.GetComponent<MeshRenderer>();
                kiteDotMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                if (kiteDotMaterial.HasColor("_BaseColor")) kiteDotMaterial.SetColor("_BaseColor", kiteVisualColor);
                if (kiteDotMaterial.HasColor("_Color")) kiteDotMaterial.SetColor("_Color", kiteVisualColor);
                mr.sharedMaterial = kiteDotMaterial;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }
        }

    }
}
