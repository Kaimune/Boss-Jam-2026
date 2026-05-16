using System;
using System.Collections.Generic;
using UnityEngine;

namespace BossJam.GridSystem
{
    /// <summary>
    /// Cell-occupancy ledger and verdict-based collision resolution for a grid.
    /// Pure logic — owns no scene state, transforms, or rendering. Held by BossGrid.
    ///
    /// Three data structures:
    ///   occupants        — which entities are touching cell (x, y)?
    ///   ownedCells       — which cells is this entity touching right now? (inverse of occupants, fast unregister)
    ///   currentOverlaps  — which OTHER entities was this entity overlapping last commit?
    ///                       (powers fire-on-enter-only effects)
    ///
    /// Register runs in three phases:
    ///   1. Resolve  — read-only verdict gathering, with new-entry diff vs currentOverlaps.
    ///   2. Commit   — atomic occupancy + ownedCells + currentOverlaps update.
    ///   3. Effects  — Verdict.Apply lambdas run (try/catch isolated).
    /// </summary>
    public class CollisionDetection
    {
        private readonly int width;
        private readonly int height;

        private readonly Dictionary<Vector2Int, List<GridFootprint>> occupants = new();
        private readonly Dictionary<GridFootprint, HashSet<Vector2Int>> ownedCells = new();
        // Per-mover record of which OTHER footprints it overlapped last commit.
        // Diff against this set means Verdict.Apply fires once on entry, not every frame of overlap.
        private readonly Dictionary<GridFootprint, HashSet<GridFootprint>> currentOverlaps = new();

        public CollisionDetection(int width, int height)
        {
            this.width = width;
            this.height = height;
        }

        // ---------------------------------------------------------------- public read-only

        /// All cells with at least one occupant. For debug iteration / gizmos.
        public IEnumerable<Vector2Int> OccupiedCells => occupants.Keys;

        /// Count of cells with at least one occupant.
        public int OccupiedCellCount => occupants.Count;

        public bool InBoundsCell(Vector2Int cell) =>
            cell.x >= 0 && cell.x < width && cell.y >= 0 && cell.y < height;

        public bool InBounds(Vector2 anchor, Vector2 footprint)
        {
            if (footprint.x <= 0 || footprint.y <= 0) return false;
            return anchor.x >= 0 && anchor.y >= 0
                && anchor.x + footprint.x <= width
                && anchor.y + footprint.y <= height;
        }

        // ---------------------------------------------------------------- public mutation

        /// <summary>
        /// Try to place `entity` at `anchor`. Returns false (no mutation) if any overlapping
        /// other returns Verdict.Block, or if out of bounds. Otherwise commits the new occupancy
        /// and runs Verdict.Apply effects for newly-overlapping others.
        /// </summary>
        public bool Register(GridFootprint entity, Vector2 anchor)
        {
            var (blocked, effects, newOverlaps) = Resolve(entity, anchor);
            if (blocked) return false;

            // Free old cells (keep currentOverlaps so the diff already saw the previous frame).
            FreeOwnedCells(entity);

            IntersectingCellRange(anchor, entity.Footprint, out int xMin, out int xMax, out int yMin, out int yMax);
            var owned = new HashSet<Vector2Int>();
            for (int cx = xMin; cx <= xMax; cx++)
            for (int cy = yMin; cy <= yMax; cy++)
            {
                var cell = new Vector2Int(cx, cy);
                if (!occupants.TryGetValue(cell, out var list))
                    occupants[cell] = list = new List<GridFootprint>();
                list.Add(entity);
                owned.Add(cell);
            }
            ownedCells[entity] = owned;
            currentOverlaps[entity] = newOverlaps;

            if (effects != null)
            {
                foreach (var apply in effects)
                {
                    try { apply(); }
                    catch (Exception e) { Debug.LogException(e); }
                }
            }
            return true;
        }

        /// <summary>
        /// Fully remove `entity` from the grid (cells AND overlap history).
        /// Called when an entity is disabled or destroyed.
        /// </summary>
        public void Unregister(GridFootprint entity)
        {
            FreeOwnedCells(entity);
            currentOverlaps.Remove(entity);
        }

        // ---------------------------------------------------------------- internals

        /// <summary>
        /// Read-only verdict gathering. Returns:
        ///   blocked     — true if any other returned Verdict.Block (early-exit on first such)
        ///   effects     — Apply lambdas for ONLY newly-overlapping others (enter-only)
        ///   newOverlaps — every distinct `other` we overlapped this pass (for next-frame diff)
        /// </summary>
        private (bool blocked, List<Action> effects, HashSet<GridFootprint> newOverlaps) Resolve(GridFootprint mover, Vector2 anchor)
        {
            if (!InBounds(anchor, mover.Footprint)) return (true, null, null);

            List<Action> effects = null;
            var newOverlaps = new HashSet<GridFootprint>();
            currentOverlaps.TryGetValue(mover, out var prevOverlaps);
            var moverOwner = mover.Owner;

            IntersectingCellRange(anchor, mover.Footprint, out int xMin, out int xMax, out int yMin, out int yMax);
            for (int cx = xMin; cx <= xMax; cx++)
            for (int cy = yMin; cy <= yMax; cy++)
            {
                var cell = new Vector2Int(cx, cy);
                if (!occupants.TryGetValue(cell, out var here)) continue;
                foreach (var other in here)
                {
                    if (other == mover) continue;
                    // Dedupe — same `other` can be touched via multiple cells; resolve once.
                    if (!newOverlaps.Add(other)) continue;

                    var verdict = other.Owner != null
                        ? other.Owner.OnEnteredBy(moverOwner)
                        : Verdict.Block;
                    if (verdict.Blocks) return (true, null, null);

                    var isNewEntry = prevOverlaps == null || !prevOverlaps.Contains(other);
                    if (verdict.Apply != null && isNewEntry)
                        (effects ??= new List<Action>()).Add(verdict.Apply);
                }
            }
            return (false, effects, newOverlaps);
        }

        /// Removes the entity from cell maps only. Leaves currentOverlaps intact so an
        /// in-progress Register can still consult the previous frame's overlap set.
        private void FreeOwnedCells(GridFootprint entity)
        {
            if (!ownedCells.TryGetValue(entity, out var owned)) return;
            foreach (var cell in owned)
            {
                if (!occupants.TryGetValue(cell, out var list)) continue;
                list.Remove(entity);
                if (list.Count == 0) occupants.Remove(cell);
            }
            ownedCells.Remove(entity);
        }

        /// Integer cell range that the rectangle [anchor, anchor+footprint) intersects.
        /// "Any amount on tile" — a cell counts if the rectangle touches it at all.
        private static void IntersectingCellRange(Vector2 anchor, Vector2 footprint,
            out int xMin, out int xMax, out int yMin, out int yMax)
        {
            xMin = Mathf.FloorToInt(anchor.x);
            xMax = Mathf.CeilToInt(anchor.x + footprint.x) - 1;
            yMin = Mathf.FloorToInt(anchor.y);
            yMax = Mathf.CeilToInt(anchor.y + footprint.y) - 1;
        }
    }
}
