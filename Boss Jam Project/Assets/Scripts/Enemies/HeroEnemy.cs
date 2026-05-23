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
        [Tooltip("Wave (1-based) at which the hero gains the matching ability. Melee is always on.")]
        [SerializeField, Min(1)] private int fireballUnlockWave = 2;
        [SerializeField, Min(1)] private int dodgeUnlockWave = 3;

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
                HeroKilledStatic?.Invoke();
                Destroy(gameObject);
                return;
            }
            BeginHitReaction();
        }

        // ── Hit reaction ─────────────────────────────────────────────
        [Header("Hit reaction")]
        [Tooltip("Seconds the hero is shoved in the random push direction after a non-killing hit.")]
        [SerializeField, Min(0f)] private float pushDurationSeconds = 0.15f;
        [Tooltip("Seconds the hero is stunned (no kiting, no abilities) after a non-killing hit. Push runs inside this window.")]
        [SerializeField, Min(0.05f)] private float stunDurationSeconds = 0.5f;
        [Tooltip("Iframe window granted after a hit. Usually matches stun so chained boss hits can't connect mid-stun.")]
        [SerializeField, Min(0f)] private float iframesOnHitSeconds = 0.5f;

        private float invulnUntil = -1f;
        private float stunUntil = -1f;
        private float pushUntil = -1f;
        private Vector2 pushDir;

        public bool IsInvulnerable => Time.time < invulnUntil;
        public bool IsStunned => Time.time < stunUntil;

        public void SetInvulnFor(float seconds)
        {
            if (seconds <= 0f) return;
            var until = Time.time + seconds;
            if (until > invulnUntil) invulnUntil = until;
        }

        private void BeginHitReaction()
        {
            int pick = UnityEngine.Random.Range(0, 4);
            pushDir = pick switch
            {
                0 => Vector2.up,
                1 => Vector2.down,
                2 => Vector2.left,
                _ => Vector2.right,
            };
            pushUntil = Time.time + pushDurationSeconds;
            stunUntil = Time.time + stunDurationSeconds;
            SetInvulnFor(iframesOnHitSeconds);

            // Abort any in-progress ability so the brain isn't mid-dodge or
            // mid-fireball-spawn when control resumes. Brain.CancelAll calls
            // Cancel() on each registered ability.
            if (brain != null) brain.CancelAll();
        }

        private GridMover mover;
        private GridFootprint targetFootprint;
        private float stuckTimer;
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
            if (rt != null) rt.Hero = this;

            // HP is snapshotted at spawn: debuffs that later raise/lower HeroMaxHp
            // affect the NEXT hero, not this living one.
            spawnedMaxHp = EffI(Target.HeroMaxHp, config.maxHp);
            currentHp = spawnedMaxHp;

            mover = GetComponent<GridMover>();
            if (grid == null && Footprint != null) grid = Footprint.Grid;

            // When spawned from a prefab the scene-bound target ref is null —
            // resolve to the boss in the scene.
            bossRef = FindFirstObjectByType<BossJam.Player.BossController>();
            if (target == null && bossRef != null) target = bossRef.transform;

            // Auto-resolve ability components if the inspector didn't wire them.
            if (brain == null)    brain    = GetComponent<HeroBrain>();
            if (melee == null)    melee    = GetComponent<HeroMelee>();
            if (fireball == null) fireball = GetComponent<HeroFireball>();
            if (dodge == null)    dodge    = GetComponent<HeroDodge>();

            // Wave-based loadout. Wave 1 = melee only; later waves unlock more.
            int wave = rt != null ? rt.CurrentWaveIndex : 1;
            if (brain != null)
            {
                if (melee != null) brain.RegisterAbility(melee);
                if (fireball != null && wave >= fireballUnlockWave) brain.RegisterAbility(fireball);
                if (dodge != null && wave >= dodgeUnlockWave) brain.RegisterAbility(dodge);

                // Disable locked-ability components so their internal state
                // (cooldown timers, OnEnable side effects) doesn't tick during
                // waves where the brain ignores them.
                if (fireball != null) fireball.enabled = wave >= fireballUnlockWave;
                if (dodge != null) dodge.enabled = wave >= dodgeUnlockWave;
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
                float speed = config.moveSpeed * Mathf.Max(0.01f, config.moveSpeedMultiplier);
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
            if (target == null) { mover.InputDirection = Vector2.zero; return; }

            // Stun gate: brain ignored, kiting paused. Push direction drives
            // the mover for the first slice of the stun, then the hero sits
            // until iframes/stun lapse together.
            if (IsStunned)
            {
                mover.InputDirection = (Time.time < pushUntil) ? pushDir : Vector2.zero;
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
            mover.InputDirection = inputDir;
            ApplyDodgeSpeedIfChanged();
            TickStuckDetector();
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
            return new HeroDecisionContext
            {
                heroCenter         = myCenter,
                bossCenter         = predictedBossCenter,
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
                Footprint.Footprint, orbitSign);

            kiteTarget = r.TargetPoint;
            hasKiteTarget = r.ValidTargetFound;
            return r.Direction;
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
