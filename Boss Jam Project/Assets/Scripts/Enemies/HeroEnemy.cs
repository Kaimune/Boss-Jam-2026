using BossJam.GridSystem;
using BossJam.Player;
using UnityEngine;
using UnityEngine.Serialization;

namespace BossJam.Enemies
{
    /// <summary>
    /// AI-driven hero. Kites the boss at a preferred distance while strafing,
    /// and periodically lobs fireballs at it. Takes damage from boss hitboxes.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(GridFootprint))]
    [RequireComponent(typeof(GridMover))]
    public class HeroEnemy : MonoBehaviour, IGridEntity, IDamageable
    {
        [Header("Refs")]
        [SerializeField] private Transform target;
        [SerializeField] private Fireball fireballPrefab;
        [SerializeField] private BossGrid grid;

        [Header("HP")]
        [SerializeField, Min(1), FormerlySerializedAs("hp")] private int maxHp = 3;
        private int currentHp;

        public int MaxHp => maxHp;
        public int CurrentHp => currentHp;
        public event System.Action<int, int> HpChanged;

        [Header("Tick")]
        [Tooltip("Drives GridMover speed. <1 = faster than tick baseline, >1 = slower.")]
        [SerializeField, Min(0.01f)] private float tickMultiplier = 1f;

        [Header("Kiting")]
        [Tooltip("Distance in cells the hero tries to maintain from the boss.")]
        [SerializeField, Min(0.5f)] private float preferredDistanceCells = 8f;
        [Tooltip("+1 rotates the off-grid search CCW around the boss, -1 CW. Flipped when stuck.")]
        [SerializeField] private int orbitSign = 1;
        [Tooltip("If movement is requested but blocked for this long, flip orbit direction.")]
        [SerializeField, Min(0.05f)] private float stuckFlipSeconds = 0.25f;

        [Header("Reaction")]
        [Tooltip("How long the hero is fooled by a sudden direction change. Under steady motion the prediction cancels this lag out.")]
        [SerializeField, Min(0f)] private float reactionTimeSeconds = 0.25f;
        [Tooltip("Velocity-estimate smoothing window. Bigger = steadier estimate but slower to pick up new motion.")]
        [SerializeField, Min(0.01f)] private float velocityWindowSeconds = 0.1f;
        [Tooltip("Optional cap on extrapolation magnitude (cells). 0 disables. Stops teleports / huge spikes from launching the predicted point off the map.")]
        [SerializeField, Min(0f)] private float maxExtrapolationCells = 0f;
        [Tooltip("How far back perception samples are retained. Should comfortably exceed reaction + velocity window.")]
        [SerializeField, Min(0.1f)] private float perceptionBufferSeconds = 1f;

        [Header("Debug")]
        [SerializeField] private bool drawKiteVisual = true;
        [SerializeField] private Color kiteVisualColor = new Color(0.2f, 1f, 0.6f, 0.9f);
        [SerializeField, Min(0.01f)] private float kiteLineWidth = 0.08f;
        [SerializeField, Min(0.05f)] private float kiteDotScale = 0.35f;
        [Tooltip("World-space Y lift so the visual draws above the ground plane.")]
        [SerializeField, Min(0f)] private float kiteVisualYLift = 0.1f;

        [Header("Fireball")]
        [SerializeField, Min(0.1f)] private float fireballIntervalSeconds = 2.5f;
        [SerializeField] private Vector2 fireballSize = Vector2.one;
        [Tooltip("Delay before the first shot after spawning.")]
        [SerializeField, Min(0f)] private float firstShotDelay = 1f;

        private GridFootprint cachedFootprint;
        public GridFootprint Footprint =>
            cachedFootprint != null ? cachedFootprint : (cachedFootprint = GetComponent<GridFootprint>());

        public float TickMultiplier => tickMultiplier;
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
            currentHp = Mathf.Max(0, currentHp - amount);
            Debug.Log($"HeroEnemy '{name}' took {amount} damage (hp={currentHp})");
            HpChanged?.Invoke(currentHp, maxHp);
            if (currentHp <= 0) Destroy(gameObject);
        }

        private GridMover mover;
        private GridFootprint targetFootprint;
        private float nextShotTime;
        private float stuckTimer;
        private Vector2 kiteTarget;
        private bool hasKiteTarget;
        private BossPredictor predictor;
        private Vector2 predictedBossCenter;

        // Runtime debug visual (visible in Game view without needing Gizmos toggle).
        private LineRenderer kiteLine;
        private Transform kiteDot;
        private Material kiteLineMaterial;
        private Material kiteDotMaterial;

        private void Awake()
        {
            currentHp = maxHp;
            mover = GetComponent<GridMover>();
            if (grid == null && Footprint != null) grid = Footprint.Grid;

            float buffer = Mathf.Max(perceptionBufferSeconds, reactionTimeSeconds + velocityWindowSeconds + 0.1f);
            predictor = new BossPredictor(buffer)
            {
                ReactionTimeSeconds = reactionTimeSeconds,
                VelocityWindowSeconds = velocityWindowSeconds,
                MaxExtrapolationCells = maxExtrapolationCells,
            };
        }

        private void OnEnable() => ApplyTick();
        private void OnValidate() => ApplyTick();

        private void ApplyTick()
        {
            foreach (var t in GetComponentsInChildren<ITickScalable>(includeInactive: true))
                t.ApplyTick(tickMultiplier);
        }

        private void Start()
        {
            // Awake-order between root GameObjects is undefined; resolve in Start.
            if (target != null) targetFootprint = target.GetComponent<GridFootprint>();
            nextShotTime = Time.time + firstShotDelay;
            HpChanged?.Invoke(currentHp, maxHp);
        }

        private void Update()
        {
            if (target == null) { mover.InputDirection = Vector2.zero; return; }

            UpdatePerception();
            mover.InputDirection = ComputeSteering();
            TickStuckDetector();
            TickFireball();
        }

        private void UpdatePerception()
        {
            Vector2 realBossCenter = (targetFootprint != null)
                ? targetFootprint.Anchor + targetFootprint.Footprint * 0.5f
                : WorldToCellCenter(target.position);
            predictor.Observe(Time.time, realBossCenter);
            predictedBossCenter = predictor.Predict(Time.time, realBossCenter);
        }

        private Vector2 ComputeSteering()
        {
            Vector2 myCenter = Footprint.Anchor + Footprint.Footprint * 0.5f;

            var r = HeroKiteSteering.Solve(
                myCenter, predictedBossCenter, preferredDistanceCells, grid,
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

            if (stuckTimer >= stuckFlipSeconds)
            {
                orbitSign = -orbitSign;
                stuckTimer = 0f;
            }
        }

        private void TickFireball()
        {
            if (fireballPrefab == null || grid == null) return;
            if (Time.time < nextShotTime) return;
            nextShotTime = Time.time + fireballIntervalSeconds;
            SpawnFireball();
        }

        private void SpawnFireball()
        {
            Vector2 anchor = Footprint.Anchor;
            Vector3 worldPos = grid.FootprintCenterWorld(anchor, fireballSize);

            // Aim at the predicted boss position (lag + extrapolation), so juking
            // dodges shots the same way it dodges movement.
            Vector2 myCenter = Footprint.Anchor + Footprint.Footprint * 0.5f;
            Vector2 dir = predictedBossCenter - myCenter;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector2.left;
            dir.Normalize();

            Fireball instance = Instantiate(fireballPrefab);
            instance.gameObject.SetActive(false);
            instance.transform.position = worldPos;

            GridFootprint fp = instance.GetComponent<GridFootprint>();
            if (fp != null) fp.Configure(anchor, fireballSize, grid);

            instance.Direction = dir;
            instance.gameObject.SetActive(true);
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

        private void OnDestroy()
        {
            if (kiteLineMaterial != null) Destroy(kiteLineMaterial);
            if (kiteDotMaterial != null) Destroy(kiteDotMaterial);
        }
    }
}
