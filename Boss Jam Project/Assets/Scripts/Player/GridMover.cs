using BossJam.GridSystem;
using UnityEngine;

namespace BossJam.Player
{
    [DisallowMultipleComponent]
    public class GridMover : MonoBehaviour, ITickScalable
    {
        [Header("Grid (use one)")]
        [Tooltip("Custom bounded grid (preferred). If set, drives movement using this entity's GridFootprint.")]
        [SerializeField] private BossGrid bossGrid;

        [Tooltip("Legacy Unity Grid. Used only when bossGrid is unset.")]
        [SerializeField] private Grid grid;

        public Vector2 AnchorPosition { get; private set; }
        public Vector2Int Facing { get; private set; } = new Vector2Int(1, 0);
        public bool IsMoving { get; private set; }

        // Continuous direction (-1..1 per axis). Set externally each frame.
        public Vector2 InputDirection { get; set; }

        private GridFootprint footprint;
        private float tickScale = 1f;

        public void ApplyTick(float tickMultiplier) => tickScale = tickMultiplier;

        // Cells per second. 1 cell per tick by baseline; tickMultiplier scales duration.
        private float CellsPerSecond
        {
            get
            {
                if (bossGrid == null) return 8.33f;
                var dur = bossGrid.TickDuration * tickScale;
                return dur > 0.0001f ? 1f / dur : 8.33f;
            }
        }

        private bool UseBossGrid => bossGrid != null;

        private void Awake()
        {
            footprint = GetComponent<GridFootprint>();

            if (UseBossGrid)
            {
                if (footprint == null)
                {
                    Debug.LogError($"{nameof(GridMover)} on '{name}' uses BossGrid but is missing a GridFootprint component.", this);
                    enabled = false;
                }
                return;
            }

            if (grid == null) grid = GetComponentInParent<Grid>();
            if (grid == null)
            {
                Debug.LogError($"{nameof(GridMover)} on '{name}' requires a BossGrid (with GridFootprint) or a Unity Grid in parents.", this);
                enabled = false;
                return;
            }
            var startCell = grid.WorldToCell(transform.position);
            AnchorPosition = new Vector2(startCell.x, startCell.y);
            transform.position = grid.GetCellCenterWorld(startCell);
        }

        private void Start()
        {
            if (!UseBossGrid || footprint == null) return;
            AnchorPosition = footprint.Anchor;
            transform.position = bossGrid.FootprintCenterWorld(footprint.Anchor, footprint.Footprint);
        }

        private void Update()
        {
            if (!UseBossGrid) return;
            if (footprint == null) return;

            var dir = InputDirection;
            var moved = false;

            if (dir != Vector2.zero)
            {
                var delta = dir * (CellsPerSecond * Time.deltaTime);

                // X first, then Y. Splitting axes gives slide-along-wall for free.
                if (Mathf.Abs(delta.x) > 0f)
                {
                    var target = new Vector2(AnchorPosition.x + delta.x, AnchorPosition.y);
                    if (footprint.TryMoveTo(target))
                    {
                        AnchorPosition = target;
                        Facing = new Vector2Int(delta.x > 0 ? 1 : -1, 0);
                        moved = true;
                    }
                }

                if (Mathf.Abs(delta.y) > 0f)
                {
                    var target = new Vector2(AnchorPosition.x, AnchorPosition.y + delta.y);
                    if (footprint.TryMoveTo(target))
                    {
                        AnchorPosition = target;
                        Facing = new Vector2Int(0, delta.y > 0 ? 1 : -1);
                        moved = true;
                    }
                }
            }

            IsMoving = moved;
            transform.position = bossGrid.FootprintCenterWorld(AnchorPosition, footprint.Footprint);
        }

        public void SnapToAnchor(Vector2 anchor)
        {
            AnchorPosition = anchor;
            if (UseBossGrid)
            {
                footprint.TryMoveTo(anchor);
                transform.position = bossGrid.FootprintCenterWorld(anchor, footprint.Footprint);
            }
            else
            {
                var cell = new Vector3Int(Mathf.FloorToInt(anchor.x), Mathf.FloorToInt(anchor.y), 0);
                transform.position = grid.GetCellCenterWorld(cell);
            }
        }
    }
}
