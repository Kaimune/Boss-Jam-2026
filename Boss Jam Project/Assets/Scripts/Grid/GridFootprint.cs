using UnityEngine;

namespace BossJam.GridSystem
{
    [DisallowMultipleComponent]
    public class GridFootprint : MonoBehaviour
    {
        [Tooltip("Cells occupied (width × height). For a 3×3 boss, set to (3,3).")]
        [SerializeField] private Vector2Int footprint = new Vector2Int(1, 1);

        [Tooltip("Starting cell of the bottom-left of the footprint.")]
        [SerializeField] private Vector2Int initialAnchor = Vector2Int.zero;

        [SerializeField] private BossGrid grid;

        public Vector2Int Footprint => footprint;
        public Vector2Int Anchor { get; private set; }
        public BossGrid Grid => grid;

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

        public bool TryMoveTo(Vector2Int newAnchor)
        {
            if (grid == null) return false;
            if (!grid.Register(this, newAnchor)) return false;
            Anchor = newAnchor;
            return true;
        }
    }
}
