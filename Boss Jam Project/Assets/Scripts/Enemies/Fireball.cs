using BossJam.GridSystem;
using UnityEngine;

namespace BossJam.Enemies
{
    /// <summary>
    /// Simple enemy projectile. Moves in a fixed direction at constant speed.
    /// Reaches the boss → boss's OnEnteredBy damages itself + destroys this fireball.
    /// Goes out of bounds → self-destructs.
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

        // IGridEntity
        private GridFootprint cachedFootprint;
        public GridFootprint Footprint =>
            cachedFootprint != null ? cachedFootprint : (cachedFootprint = GetComponent<GridFootprint>());
        public float TickMultiplier => tickMultiplier;
        public Team Team => Team.Enemy;
        public Verdict OnEnteredBy(IGridEntity mover) => Verdict.Pass;

        // ITickScalable — could drive speed; default-1 keeps absolute cellsPerSecond.
        private float tickMultiplier = 1f;
        public void ApplyTick(float m) => tickMultiplier = m;

        private void Start()
        {
            ApplyVisualSize();
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

            var delta = Direction * (cellsPerSecond * tickMultiplier * Time.deltaTime);
            var target = fp.Anchor + delta;

            // Out of grid bounds → vanish.
            if (!grid.InBounds(target, fp.Footprint))
            {
                Destroy(gameObject);
                return;
            }

            if (!fp.TryMoveTo(target))
            {
                // Move was blocked. Receiver's Apply may have destroyed us already; if not,
                // we're now stuck against something solid — vanish gracefully.
                Destroy(gameObject);
                return;
            }

            transform.position = grid.FootprintCenterWorld(fp.Anchor, fp.Footprint);
        }
    }
}
