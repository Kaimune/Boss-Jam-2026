using System;
using BossJam.GridSystem;
using DG.Tweening;
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

        [Header("Tween")]
        [SerializeField] private Ease ease = Ease.OutQuad;

        public Vector3Int CellPosition { get; private set; }
        public Vector2Int Facing { get; private set; } = new Vector2Int(1, 0);
        public bool IsMoving { get; private set; }

        public event Action StepCompleted;

        private GridFootprint footprint;
        private Tweener activeTween;

        private float tickScale = 1f;
        public void ApplyTick(float tickMultiplier) => tickScale = tickMultiplier;

        private float EffectiveStepDuration =>
            bossGrid != null ? bossGrid.TickDuration * tickScale : 0.12f;

        private bool UseBossGrid => bossGrid != null;

        private void Awake()
        {
            footprint = GetComponent<GridFootprint>();

            if (UseBossGrid)
            {
                // GridFootprint owns initial placement & registration; sync happens in Start
                // because GridFootprint.OnEnable hasn't run yet when Awake fires.
                if (footprint == null)
                {
                    Debug.LogError($"{nameof(GridMover)} on '{name}' uses BossGrid but is missing a GridFootprint component.", this);
                    enabled = false;
                }
                return;
            }

            // Legacy path: Unity Grid + 1×1.
            if (grid == null) grid = GetComponentInParent<Grid>();
            if (grid == null)
            {
                Debug.LogError($"{nameof(GridMover)} on '{name}' requires a BossGrid (with GridFootprint) or a Unity Grid in parents.", this);
                enabled = false;
                return;
            }
            CellPosition = grid.WorldToCell(transform.position);
            transform.position = grid.GetCellCenterWorld(CellPosition);
        }

        private void Start()
        {
            if (!UseBossGrid || footprint == null) return;
            // GridFootprint.OnEnable has now run and placed the footprint at its initialAnchor.
            // Mirror that anchor and snap to the footprint center so the boss starts in the right place.
            CellPosition = (Vector3Int)footprint.Anchor;
            transform.position = bossGrid.FootprintCenterWorld(footprint.Anchor, footprint.Footprint);
        }

        private void OnDisable()
        {
            activeTween?.Kill();
            activeTween = null;
            IsMoving = false;
        }

        public bool TryStep(Vector2Int gridDelta)
        {
            if (IsMoving) return false;
            if (gridDelta == Vector2Int.zero) return false;

            Facing = gridDelta;

            var targetCell = new Vector2Int(CellPosition.x + gridDelta.x, CellPosition.y + gridDelta.y);

            if (UseBossGrid)
            {
                if (!footprint.TryMoveTo(targetCell)) return false;
                CellPosition = (Vector3Int)targetCell;
                IsMoving = true;
                var worldTarget = bossGrid.FootprintCenterWorld(targetCell, footprint.Footprint);
                activeTween = transform.DOMove(worldTarget, EffectiveStepDuration)
                    .SetEase(ease)
                    .OnComplete(OnTweenComplete);
                return true;
            }

            var target3 = new Vector3Int(targetCell.x, targetCell.y, 0);
            if (!IsCellPassable(target3)) return false;
            CellPosition = target3;
            IsMoving = true;
            var worldTargetLegacy = grid.GetCellCenterWorld(target3);
            activeTween = transform.DOMove(worldTargetLegacy, EffectiveStepDuration)
                .SetEase(ease)
                .OnComplete(OnTweenComplete);
            return true;
        }

        private void OnTweenComplete()
        {
            IsMoving = false;
            activeTween = null;
            StepCompleted?.Invoke();
        }

        public void SnapToCell(Vector3Int cell)
        {
            activeTween?.Kill();
            activeTween = null;
            IsMoving = false;
            CellPosition = cell;
            if (UseBossGrid)
            {
                var anchor = new Vector2Int(cell.x, cell.y);
                footprint.TryMoveTo(anchor);
                transform.position = bossGrid.FootprintCenterWorld(anchor, footprint.Footprint);
            }
            else
            {
                transform.position = grid.GetCellCenterWorld(cell);
            }
        }

        protected virtual bool IsCellPassable(Vector3Int cell) => true;
    }
}
