using System.Collections.Generic;
using BossJam.Audio;
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
        [SerializeField] private HeroDashStrike dashStrike;
        [SerializeField] private HeroFireball fireball;
        [SerializeField] private HeroDodge dodge;

        [Tooltip("Optional hit-feedback bundle (SFX + hurt animator state). Fires when HP actually drops.")]
        [SerializeField] private HitReactionFx hitFx;
        [Tooltip("Optional death-feedback bundle (SFX + death animator state). Fires once on the lethal blow. Outro waits its clip length.")]
        [SerializeField] private DeathFx deathFx;
        public DeathFx DeathFx => deathFx;

        // Public read-only views the ability components query each frame.
        public HeroConfig Config => config;
        public BossGrid Grid => grid;

        private int currentHp;
        private int spawnedMaxHp;

        public int MaxHp => spawnedMaxHp;
        public int CurrentHp => currentHp;
        public bool IsDead => currentHp <= 0;
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
            // Corpse can't take more damage — re-entering would re-fire
            // HeroKilledStatic and re-trigger the outro cascade.
            if (currentHp <= 0) return;
            // Block in-flight damage during the cinematic. The world keeps
            // ticking but no kill-shots can land while the outro plays.
            if (BossJam.Game.GameStateController.Instance != null
                && BossJam.Game.GameStateController.Instance.State != BossJam.Game.GameState.Playing) return;
            if (IsInvulnerable) return;
            currentHp = Mathf.Max(0, currentHp - amount);
            Debug.Log($"HeroEnemy '{name}' took {amount} damage (hp={currentHp})");
            HpChanged?.Invoke(currentHp, spawnedMaxHp);
            // Lethal blow plays DeathFx (below), not hitFx — the hurt anim
            // would otherwise briefly clobber the death state on the same frame.
            if (hitFx != null && currentHp > 0) hitFx.Play();
            // Hit-stop: brief global freeze on landing a non-lethal hit. Skipped
            // on the lethal blow so the death cinematic owns that beat.
            if (currentHp > 0) BossJam.Game.HitStopController.Freeze();
            if (currentHp <= 0)
            {
                if (TryConsumeRespawnOnLethal(amount))
                {
                    HpChanged?.Invoke(currentHp, spawnedMaxHp);
                    return;
                }
                if (deathFx != null) deathFx.Play();
                HeroKilledStatic?.Invoke();
                // Hero NEVER despawns from its own death code — the dialogue
                // cam needs a body to frame for the final lines. ReloadScene
                // (after the outro) is the sole owner of hero disposal.
                // Silent-death tiers (HasNextTier == false → no outro fires)
                // leave a corpse on the grid intentionally.
                return;
            }
            BeginHitReaction();
        }

        // ── Hit reaction ─────────────────────────────────────────────
        // Two-phase model so the player can combo:
        //   T=0       hit lands. Hero stunned, NO iframes, NO push yet.
        //   T=combo   big knockback fires + iframes start. The combo window
        //             between T=0 and T=combo is when chained player hits
        //             can stack damage.
        [Header("Hit reaction")]
        [Tooltip("Combo window after a hit. Hero is stunned but vulnerable — chained player attacks landing in this window all do damage.")]
        [SerializeField, Min(0f)] private float comboWindowSeconds = 0.5f;
        [Tooltip("Total stun duration after the boss lands a hit. The hero stays planted (no input, no abilities) for this long before the AI re-engages. Independent of comboWindowSeconds, which controls when chained-hit damage stops stacking.")]
        [SerializeField, Min(0f)] private float stunSecondsAfterHit = 2f;

        private float invulnUntil = -1f;
        private float stunUntil = -1f;
        private float knockbackFiresAt = -1f;
        private float attackLockoutUntil = -1f;

        public bool IsInvulnerable => Time.time < invulnUntil;
        public bool IsStunned => Time.time < stunUntil;
        // Post-hit attack lockout. While true, offensive abilities (dash-strike,
        // fireball) skip themselves so the hero can't immediately retaliate
        // after eating a boss attack. Defensive abilities (dodge, kiting) are
        // unaffected. Armed by BeginHitReaction.
        public bool IsAttackLocked => Time.time < attackLockoutUntil;

        [Tooltip("How long after taking a hit the hero cannot use offensive abilities (dash-strike, fireball). Kiting and dodge stay active. Set to 0 to disable the lockout.")]
        [SerializeField, Min(0f)] private float postHitAttackLockoutSeconds = 1.5f;

        /// <summary>
        /// Extend (never shorten) the offensive-ability lockout by `seconds`
        /// from now. Used both for post-hit recovery on the hero AND post-hit
        /// breathing room after the hero lands a strike on the boss.
        /// </summary>
        public void LockoutAttacksFor(float seconds)
        {
            if (seconds <= 0f) return;
            float until = Time.time + seconds;
            if (until > attackLockoutUntil) attackLockoutUntil = until;
        }

        public void SetInvulnFor(float seconds)
        {
            if (seconds <= 0f) return;
            var until = Time.time + seconds;
            if (until > invulnUntil) invulnUntil = until;
        }

        // Save-scum gate. Tiers with HeroRespawnMode != None grant the hero a
        // one-shot revival per attempt (boss death re-arms the token). The
        // Tutorial tier is special-cased: infinite revivals so the player can
        // experiment without losing the run.
        private bool TrySpendSaveScum()
        {
            if (rt == null) return false;
            if (rt.Flags.HeroRespawnMode == HeroRespawnMode.None) return false;

            bool isTutorial = rt.CurrentTierName == "Tutorial";
            if (isTutorial) return true; // unlimited — never consume the token

            if (rt.State == null || !rt.State.saveScumAvailable) return false;
            rt.State.saveScumAvailable = false;
            return true;
        }

        private bool TryConsumeRespawnOnLethal(int incomingDamage)
        {
            // The hero's hp just hit 0 from a lethal blow (which may have landed
            // during the 1-HP warning window or directly from full hp on a high
            // damage roll). If save-scum is still available for this attempt,
            // short-circuit the death and trigger the reload-with-skip flow.

            // Tutorial exit gate: a kill landed while the warning window was
            // armed is what passes the tier. Skip the save-scum so the death
            // outro fires and AdvanceTier promotes us. Outside the window
            // (boss one-shot or hp > threshold) save-scum still loops the
            // tutorial — the player has to bring the hero down to the warning
            // hp range first.
            bool isTutorial = rt != null && rt.CurrentTierName == "Tutorial";
            if (isTutorial && conditionalRespawnArmedAt > 0f) return false;

            // Final tier: lethal damage ends the run. Skip the lethal-blow
            // save-scum here (which would freeze the boss + play the hero's
            // death fx, reading as a player death and looping forever). The
            // low-HP-warning reload still fires through TickConditionalRespawn
            // so the player_health_restore mechanic stays alive.
            if (rt != null && !rt.HasNextTier) return false;

            if (!TrySpendSaveScum()) return false;
            conditionalRespawnArmedAt = -1f;
            // Lethal-blow save-scum: play the death animation in place before
            // the reload so the player sees the hit register. GSC owns the
            // wait + lockdown + reload; the 1-HP-window path below still
            // uses the instant TriggerReloadRespawn since no death happened.
            var gsc = BossJam.Game.GameStateController.Instance;
            if (gsc != null) gsc.TriggerSaveScumReload(deathFx);
            else TriggerReloadRespawn();
            return true;
        }

        // Set RunState.skipNextIntro and reload the scene. Used by the warning
        // tick (1 HP for windowSeconds) and the lethal-blow save-scum trigger.
        // After reload, GameStateController.Begin sees the flag, skips intro +
        // pre-fight dialogue, drops into Playing, and plays respawn_reload.
        private void TriggerReloadRespawn()
        {
            var gsc = BossJam.Game.GameStateController.Instance;
            if (gsc == null) return;
            if (rt != null && rt.State != null) rt.State.skipNextIntro = true;
            gsc.ReloadScene();
        }

        private void BeginHitReaction()
        {
            // Hero stays planted in place for the combo window AND the brain-pause
            // after — no displacement, no iframes — so chained player attacks can
            // keep landing without the hero teleporting out of range.
            float now = Time.time;
            knockbackFiresAt = now + comboWindowSeconds;
            stunUntil = now + stunSecondsAfterHit;
            // Post-hit attack lockout — measured from the moment of impact,
            // not the end of stun, so the cooldown is predictable regardless
            // of combo length.
            if (postHitAttackLockoutSeconds > 0f)
                attackLockoutUntil = now + postHitAttackLockoutSeconds;

            // Abort any in-progress ability so the brain isn't mid-dodge or
            // mid-fireball-spawn when control resumes. Brain.CancelAll calls
            // Cancel() on each registered ability.
            if (brain != null) brain.CancelAll();
        }

        // Combo window timer expiry. No displacement and no iframes — just
        // clears the latch so Update stops calling us. The brain stays parked
        // by stunUntil until stunSecondsAfterHit fully elapses.
        private void FireDelayedKnockback()
        {
            knockbackFiresAt = -1f;
        }


        private GridMover mover;
        private GridFootprint targetFootprint;
        private float stuckTimer;

        // Tier-driven HP regen. Flags are populated by HeroRegenEffect.Apply; the
        // ticker advances during Playing state only and is suppressed at max hp.
        private float regenAccumulator;

        // FullHp warning timer — armed each time hp falls to <= RespawnArmHpThreshold,
        // cleared when the reload fires or the hero recovers above the threshold.
        // (SaveScumOnFirstOneHp is now boss-death triggered; see GameStateController.)
        private float conditionalRespawnArmedAt = -1f;

        /// <summary>True while the low-HP warning timer is counting down toward a save-scum reload.</summary>
        public bool IsRespawnWarningActive => conditionalRespawnArmedAt > 0f;

        /// <summary>Seconds remaining before the warning expires and the reload fires. 0 if not armed.</summary>
        public float RespawnWarningSecondsRemaining
        {
            get
            {
                if (rt == null || conditionalRespawnArmedAt <= 0f) return 0f;
                float remaining = (conditionalRespawnArmedAt + rt.Flags.HeroRespawnWindowSeconds) - Time.time;
                return Mathf.Max(0f, remaining);
            }
        }

        private Vector2 kiteTarget;
        private bool hasKiteTarget;
        private BossPredictor predictor;
        private Vector2 predictedBossCenter;
        private BossJam.Player.BossController bossRef;
        private bool lastBoostActive;
        private float lastBoostMul = 1f;
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
            if (rt != null) rt.Hero = this;

            // HP is snapshotted at spawn: debuffs that later raise/lower HeroMaxHp
            // affect the NEXT hero, not this living one.
            spawnedMaxHp = EffI(Target.HeroMaxHp, config.maxHp);
            currentHp = spawnedMaxHp;

            mover = GetComponent<GridMover>();
            if (grid == null && Footprint != null) grid = Footprint.Grid;
            if (visual == null) visual = transform;

            // Honour the scene-authored idle rotation as the starting facing.
            facingTarget = visual.rotation;

            // When spawned from a prefab the scene-bound target ref is null —
            // resolve to the boss in the scene.
            bossRef = FindFirstObjectByType<BossJam.Player.BossController>();
            if (target == null && bossRef != null) target = bossRef.transform;

            // Auto-resolve ability components if the inspector didn't wire them.
            if (brain == null)      brain      = GetComponent<HeroBrain>();
            if (dashStrike == null) dashStrike = GetComponent<HeroDashStrike>();
            if (fireball == null)   fireball   = GetComponent<HeroFireball>();
            if (dodge == null)      dodge      = GetComponent<HeroDodge>();

            // Each ability self-determines whether it's available this wave
            // via its IsEnabled property: an `enabledByDefault` toggle on the
            // ability script, optionally overridden by a DifficultyRuntime
            // modifier on the matching Target.*Enabled enum entry. The brain
            // only sees abilities that resolved to enabled.
            if (brain != null)
            {
                if (dashStrike != null && dashStrike.IsEnabled) brain.RegisterAbility(dashStrike);
                if (fireball != null && fireball.IsEnabled)     brain.RegisterAbility(fireball);
                if (dodge != null && dodge.IsEnabled)           brain.RegisterAbility(dodge);

                // Quiet the component so its internal state (cooldown timers,
                // OnEnable side effects) doesn't tick during waves where it's
                // disabled. Dash-strike runs lean already so we leave it alone.
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
            if (kiteLineMaterial != null) Destroy(kiteLineMaterial);
            if (kiteDotMaterial != null) Destroy(kiteDotMaterial);
        }

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

        private void ApplyTick() => ApplyTickInternal(boosted: false, boostMul: 1f);

        // boosted == true folds the supplied speed multiplier into the
        // GridMover tick so the hero physically moves faster for the boost
        // window. ApplyMovementBoostIfChanged picks the multiplier based on
        // which boost-bearing ability is active (dash-strike or dodge).
        private void ApplyTickInternal(bool boosted, float boostMul)
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
                    speed *= Mathf.Max(1f, boostMul);
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

        // True once Update has seen the first GameState.Playing frame after
        // scene load. Used to arm spawn-delay cooldowns on the *gameplay*
        // start moment instead of the scene-load moment (Awake), so a long
        // intro dialogue doesn't burn the delay before the player can react.
        private bool playingStateSeen;

        private void Update()
        {
            if (BossJam.Game.GameStateController.Instance != null &&
                BossJam.Game.GameStateController.Instance.State != BossJam.Game.GameState.Playing)
                return;

            // First gameplay frame: (re)arm spawn delays so cooldowns start
            // counting from now, after the cutscene/dialogue.
            if (!playingStateSeen)
            {
                playingStateSeen = true;
                if (dashStrike != null) dashStrike.ArmSpawnDelay();
                if (fireball != null)   fireball.ArmFirstShotDelay();
            }

            TickRegen(Time.deltaTime);
            TickConditionalRespawn();
            if (target == null) { mover.InputDirection = Vector2.zero; return; }

            // Clear the combo-window latch once it lapses.
            if (knockbackFiresAt > 0f && Time.time >= knockbackFiresAt)
                FireDelayedKnockback();

            // Stun gate: brain ignored, kiting paused. The hero plants for the
            // whole combo window + post-knockback brain pause — no displacement,
            // no iframes.
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

            // Movement override priority: dash-strike (toward boss) →
            // dodge (locked direction) → kite. Both attack abilities drive the
            // mover input; kite is the resting state.
            Vector2 inputDir;
            if (dashStrike != null && dashStrike.IsActive)
                inputDir = dashStrike.CurrentDirection;
            else if (dodge != null && dodge.IsActive)
                inputDir = dodge.LockedDirection;
            else
                inputDir = kiteDir;
            // Any ability with LocksMovement (none currently — dash-strike and
            // dodge both drive movement) would zero the input so the hero
            // plants for the clip. Kept for forward-compat.
            if (IsAbilityLockingMovement()) inputDir = Vector2.zero;
            mover.InputDirection = inputDir;
            ApplyFacing(inputDir);
            ApplyMovementBoostIfChanged();
            TickStuckDetector();
        }

        private bool IsAbilityLockingMovement()
        {
            if (fireball != null && fireball.IsBusy && fireball.LocksMovement) return true;
            // Dash-strike and dodge intentionally never lock — they DRIVE movement.
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

        // Reapply mover tick when either boost-bearing ability flips active.
        // Dash-strike takes precedence over dodge (they shouldn't overlap in
        // practice — brain marks the active one as busy — but the priority is
        // explicit here in case it ever does).
        private void ApplyMovementBoostIfChanged()
        {
            bool dashing = dashStrike != null && dashStrike.IsActive;
            bool dodging = dodge != null && dodge.IsActive;
            bool boosting = dashing || dodging;
            float mul = dashing ? dashStrike.SpeedMultiplier
                       : dodging ? dodge.SpeedMultiplier
                       : 1f;
            if (boosting == lastBoostActive && Mathf.Approximately(mul, lastBoostMul) && boosting == tickAppliedAsBoosted)
                return;
            lastBoostActive = boosting;
            lastBoostMul = mul;
            tickAppliedAsBoosted = boosting;
            ApplyTickInternal(boosting, boosting ? mul : 1f);
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

            // Dynamic preferred distance: when a punish window is open the
            // hero closes to "dash distance" — the edge of the dash's actual
            // reach (computed by HeroDashStrike from tier-adjusted move
            // speed). The dash itself then covers that last gap. Past that,
            // the dash physically can't connect, so kiting closer would just
            // produce whiffs.
            float kiteDist;
            if (bossInPunishWindowAndUnspent)
            {
                kiteDist = dashStrike != null
                    ? dashStrike.PunishEngagementDistanceCells()
                    : Eff(Target.HeroMeleeApproachDistanceCells, config.dashStrikeTriggerDistanceCells);
            }
            else
            {
                kiteDist = Eff(Target.HeroPreferredDistanceCells, config.preferredDistanceCells);
            }

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

        // Hardcoded 1 HP arm threshold — the asset's HeroRespawnThresholdHp is
        // ignored on purpose so the warning never fires at 2 HP (the previous
        // bug). Once hero sits at <= 1 HP for HeroRespawnWindowSeconds, trigger
        // the reload-with-skip flow; the hero is back at full HP on the fresh
        // scene with the respawn_reload dialogue playing.
        private const int RespawnArmHpThreshold = 1;

        private void TickConditionalRespawn()
        {
            if (rt == null) return;
            ref var f = ref rt.Flags;
            // Only the timer-based respawn (FullHpIfNotInstakilled — Tutorial /
            // Error) arms the warning UI: that's the mode where surviving the
            // window triggers the reload. SaveScumOnFirstOneHp (Beginner) is
            // boss-death triggered (see GameStateController) and has no use
            // for the countdown overlay.
            if (f.HeroRespawnMode != HeroRespawnMode.FullHpIfNotInstakilled) return;

            if (currentHp <= RespawnArmHpThreshold && conditionalRespawnArmedAt < 0f)
                conditionalRespawnArmedAt = Time.time;

            if (conditionalRespawnArmedAt > 0f && Time.time > conditionalRespawnArmedAt + f.HeroRespawnWindowSeconds)
            {
                conditionalRespawnArmedAt = -1f;
                // Boss can land a kill blow during the warning window — that
                // path goes through TryConsumeRespawnOnLethal in TakeDamage and
                // also spends the save-scum token. Here we only fire if the
                // hero survived the full window AND save-scum is still spendable.
                if (currentHp > 0 && TrySpendSaveScum()) TriggerReloadRespawn();
                return;
            }

            // Hero recovered above the threshold during the window — disarm.
            if (conditionalRespawnArmedAt > 0f && currentHp > RespawnArmHpThreshold)
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
            bool debugOn = rt == null || rt.State == null || rt.State.showDebugVisuals;
            if (!debugOn || !drawKiteVisual || !hasKiteTarget || grid == null || Footprint == null)
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
