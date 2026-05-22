using UnityEngine;

namespace BossJam.GridSystem
{
    [DisallowMultipleComponent]
    public class GridFootprint : MonoBehaviour
    {
        [Tooltip("Footprint size in cell units. Fractional values give wiggle room before walls (e.g. 5.5 vs 6).")]
        [SerializeField] private Vector2 footprint = new Vector2(1f, 1f);

        [Tooltip("Starting position of the bottom-left of the footprint, in cell-space (sub-cell allowed). Ignored when useTransformAsInitialAnchor is true.")]
        [SerializeField] private Vector2 initialAnchor = Vector2.zero;

        [Tooltip("If true, derive the initial anchor from the GameObject's world position via BossGrid.WorldToCell on enable. Use this when the entity is hand-placed in the scene and you want it to stay where you put it.")]
        [SerializeField] private bool useTransformAsInitialAnchor = false;

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
            Vector2 anchor = initialAnchor;
            if (useTransformAsInitialAnchor)
            {
                var cell = grid.WorldToCell(transform.position);
                anchor = new Vector2(cell.x, cell.y);
            }
            if (!grid.Register(this, anchor))
            {
                Debug.LogWarning($"{nameof(GridFootprint)} on '{name}' failed to register at {anchor} — out of bounds or overlapping.", this);
                return;
            }
            Anchor = anchor;
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
