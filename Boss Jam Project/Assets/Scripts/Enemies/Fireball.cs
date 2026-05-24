using BossJam.Difficulty;
using BossJam.GridSystem;
using BossJam.Player;
using UnityEngine;

namespace BossJam.Enemies
{
    /// <summary>
    /// Hero projectile. Moves in a fixed direction at constant speed by default;
    /// when the difficulty runtime's FireballHoming flag is set, re-steers toward
    /// the boss center each frame at FireballTurnRateDegPerSec.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(GridFootprint))]
    public class Fireball : MonoBehaviour, IGridEntity, ITickScalable, IDamageDealer
    {
        [Header("Motion")]
        [Tooltip("Unit-length cell-space direction. Set at spawn time.")]
        public Vector2 Direction = Vector2.left;
        [SerializeField, Min(0.1f)] private float cellsPerSecond = 6f;

        [Header("Hit")]
        [SerializeField, Min(0)] private int damage = 1;

        [Header("Visual")]
        [Tooltip("Sphere child whose localScale is set to match the footprint size at spawn.")]
        [SerializeField] private Transform visual;
        [SerializeField] private ParticleSystem trail;
        [Tooltip("Visual size as a fraction of (cellSize * max(footprint.x, footprint.y)). 0.8 looks good.")]
        [SerializeField, Range(0.1f, 2f)] private float visualScaleFactor = 0.8f;

        public int Damage => damage;
        public void SetDamage(int amount) { damage = Mathf.Max(0, amount); }

        private GridFootprint cachedFootprint;
        public GridFootprint Footprint =>
            cachedFootprint != null ? cachedFootprint : (cachedFootprint = GetComponent<GridFootprint>());
        public float TickMultiplier => tickMultiplier;
        public Team Team => Team.Enemy;
        public Verdict OnEnteredBy(IGridEntity mover) => Verdict.Pass;

        private float tickMultiplier = 1f;
        public void ApplyTick(float m) => tickMultiplier = m;

        private DifficultyRuntime rt;
        private BossController bossRef;
        private float speedMul = 1f;
        private bool homing;
        private float turnRateDegPerSec;

        private void Start()
        {
            ApplyVisualSize();

            rt = FindFirstObjectByType<DifficultyRuntime>();
            bossRef = FindFirstObjectByType<BossController>();
            if (rt != null)
            {
                ref var f = ref rt.Flags;
                speedMul = f.FireballSpeedMultiplier > 0.01f ? f.FireballSpeedMultiplier : 1f;
                homing = f.FireballHoming;
                turnRateDegPerSec = f.FireballTurnRateDegPerSec;
            }
        }

        private void ApplyVisualSize()
        {
            var fp = Footprint;
            var grid = fp != null ? fp.Grid : null;
            if (grid == null || fp == null) return;

            float worldSize = grid.CellSize * Mathf.Max(fp.Footprint.x, fp.Footprint.y) * visualScaleFactor;
            if (visual != null) visual.localScale = Vector3.one * worldSize;
            if (trail != null)
            {
                var shape = trail.shape;
                shape.radius = worldSize * 0.3f;
            }
        }

        private void Update()
        {
            var fp = Footprint;
            var grid = fp != null ? fp.Grid : null;
            if (grid == null || Direction == Vector2.zero) return;

            if (homing && bossRef != null && bossRef.Footprint != null)
            {
                Vector2 myCenter = fp.Anchor + fp.Footprint * 0.5f;
                Vector2 bossCenter = bossRef.Footprint.Anchor + bossRef.Footprint.Footprint * 0.5f;
                Vector2 desired = bossCenter - myCenter;
                if (desired.sqrMagnitude > 0.0001f)
                {
                    desired.Normalize();
                    float maxDeg = turnRateDegPerSec * Time.deltaTime;
                    Direction = TurnTowards(Direction, desired, maxDeg);
                }
            }

            var delta = Direction * (cellsPerSecond * speedMul * tickMultiplier * Time.deltaTime);
            var target = fp.Anchor + delta;

            if (!grid.InBounds(target, fp.Footprint)) { Destroy(gameObject); return; }
            if (!fp.TryMoveTo(target)) { Destroy(gameObject); return; }

            transform.position = grid.FootprintCenterWorld(fp.Anchor, fp.Footprint);
        }

        private static Vector2 TurnTowards(Vector2 from, Vector2 to, float maxDegrees)
        {
            float currentAngle = Mathf.Atan2(from.y, from.x) * Mathf.Rad2Deg;
            float targetAngle  = Mathf.Atan2(to.y,   to.x)   * Mathf.Rad2Deg;
            float newAngle     = Mathf.MoveTowardsAngle(currentAngle, targetAngle, maxDegrees);
            float r            = newAngle * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(r), Mathf.Sin(r));
        }
    }
}
