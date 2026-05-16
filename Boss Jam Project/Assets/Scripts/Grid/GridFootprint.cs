using UnityEngine;

namespace BossJam.GridSystem
{
    [DisallowMultipleComponent]
    public class GridFootprint : MonoBehaviour
    {
        [Tooltip("Footprint size in cell units. Fractional values give wiggle room before walls (e.g. 5.5 vs 6).")]
        [SerializeField] private Vector2 footprint = new Vector2(1f, 1f);

        [Tooltip("Starting position of the bottom-left of the footprint, in cell-space (sub-cell allowed).")]
        [SerializeField] private Vector2 initialAnchor = Vector2.zero;

        [SerializeField] private BossGrid grid;

        public Vector2 Footprint => footprint;
        public Vector2 Anchor { get; private set; }
        public BossGrid Grid => grid;

        private IGridEntity ownerCached;
        private bool ownerResolved;
        public IGridEntity Owner
        {
            get
            {
                if (!ownerResolved)
                {
                    ownerCached = GetComponent<IGridEntity>();
                    ownerResolved = true;
                }
                return ownerCached;
            }
        }

        private void Awake()
        {
            if (grid == null) grid = GetComponentInParent<BossGrid>();
            if (grid == null) grid = FindAnyObjectByType<BossGrid>();
        }

        private void OnEnable()
        {
            if (grid == null) return;
            if (!grid.Register(this, initialAnchor))
            {
                Debug.LogWarning($"{nameof(GridFootprint)} on '{name}' failed to register at {initialAnchor} — out of bounds or overlapping.", this);
                return;
            }
            Anchor = initialAnchor;
            transform.position = grid.FootprintCenterWorld(Anchor, footprint);
        }

        private void OnDisable()
        {
            if (grid != null) grid.Unregister(this);
        }

        public bool TryMoveTo(Vector2 newAnchor)
        {
            if (grid == null) return false;
            if (!grid.Register(this, newAnchor)) return false;
            Anchor = newAnchor;
            return true;
        }

        // Runtime configuration — call BEFORE the GameObject is activated so OnEnable
        // registers with the right values. Used by spawners that build entities in code.
        public void Configure(Vector2 initialAnchor, Vector2 footprintSize, BossGrid grid)
        {
            this.initialAnchor = initialAnchor;
            this.footprint = footprintSize;
            this.grid = grid;
        }
    }
}
