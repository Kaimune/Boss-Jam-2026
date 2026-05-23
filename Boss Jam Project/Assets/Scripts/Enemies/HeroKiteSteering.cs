using System.Collections.Generic;
using BossJam.GridSystem;
using UnityEngine;

namespace BossJam.Enemies
{
    /// <summary>
    /// Pure-function steering for a kite AI. Picks a target point at <c>preferredRadius</c>
    /// from the boss along the boss→hero axis, rotating around the boss to find an
    /// in-bounds, hazard-free spot if the natural ray exits the grid or overlaps a hazard.
    /// Returns a unit direction the hero should walk to reach that target.
    ///
    /// If no candidate on the radius-R circle is fully safe, falls back to the
    /// candidate with the smallest hazard overlap so the hero takes the optimal
    /// hit instead of freezing.
    /// </summary>
    public static class HeroKiteSteering
    {
        // Resolution of the in-bounds search when the natural kite point is off-grid.
        private const int SearchSteps = 36;
        private const float StepDegrees = 360f / SearchSteps;

        /// <summary>One hazard rect the hero should avoid. Cell-space.</summary>
        public struct HazardRect
        {
            public Vector2 Anchor;
            public Vector2 Footprint;
        }

        public struct Result
        {
            public Vector2 Direction;     // unit vector toward target (Vector2.zero if at target)
            public Vector2 TargetPoint;   // cell-space waypoint (hero center)
            public bool ValidTargetFound; // false if no in-bounds point on the radius circle was found
            public bool IsSafeTarget;     // true if the target overlaps no hazard
        }

        public static Result Solve(
            Vector2 heroCenter,
            Vector2 bossCenter,
            float preferredRadius,
            BossGrid grid,
            Vector2 heroFootprint,
            int orbitSign,
            IReadOnlyList<HazardRect> hazards = null)
        {
            Vector2 fromBoss = heroCenter - bossCenter;
            float dist = fromBoss.magnitude;
            Vector2 outDir = dist > 0.0001f ? fromBoss / dist : new Vector2(1f, 0f);

            // Preferred kite point: boss + R in the boss→hero direction.
            Vector2 preferredTarget = bossCenter + outDir * preferredRadius;
            bool preferredInBounds = IsInBoundsCenter(preferredTarget, grid, heroFootprint);
            float preferredOverlap = preferredInBounds
                ? OverlapArea(preferredTarget, heroFootprint, hazards)
                : float.PositiveInfinity;

            Vector2 best = preferredTarget;
            bool bestInBounds = preferredInBounds;
            float bestOverlap = preferredOverlap;

            // Early-out: preferred point is in-bounds AND fully safe.
            if (preferredInBounds && preferredOverlap <= 0f)
            {
                return Build(heroCenter, best, validBounds: true, safe: true);
            }

            // Walk the circle in the orbit direction looking for the first
            // fully-safe candidate, tracking the least-bad alternative along
            // the way so we have a fallback even if none are perfectly safe.
            int sign = orbitSign >= 0 ? 1 : -1;
            for (int i = 1; i <= SearchSteps; i++)
            {
                Vector2 rotated = Rotate(outDir, StepDegrees * i * sign);
                Vector2 candidate = bossCenter + rotated * preferredRadius;
                bool inBounds = IsInBoundsCenter(candidate, grid, heroFootprint);
                if (!inBounds) continue;

                float overlap = OverlapArea(candidate, heroFootprint, hazards);
                if (overlap <= 0f)
                {
                    return Build(heroCenter, candidate, validBounds: true, safe: true);
                }

                if (overlap < bestOverlap || !bestInBounds)
                {
                    best = candidate;
                    bestInBounds = true;
                    bestOverlap = overlap;
                }
            }

            // No fully-safe candidate. Return the least-overlap one we found.
            // If even the preferred point was off-grid AND no other candidate
            // was in bounds, we fall back to the preferred point (off-grid)
            // and report ValidTargetFound=false so the caller can react.
            return Build(heroCenter, best, validBounds: bestInBounds, safe: false);
        }

        private static Result Build(Vector2 heroCenter, Vector2 target, bool validBounds, bool safe)
        {
            Vector2 toTarget = target - heroCenter;
            Vector2 dir = toTarget.sqrMagnitude < 0.01f ? Vector2.zero : toTarget.normalized;
            return new Result
            {
                Direction        = dir,
                TargetPoint      = target,
                ValidTargetFound = validBounds,
                IsSafeTarget     = safe,
            };
        }

        private static bool IsInBoundsCenter(Vector2 center, BossGrid grid, Vector2 footprint)
        {
            Vector2 anchor = center - footprint * 0.5f;
            return grid != null && grid.InBounds(anchor, footprint);
        }

        // Sum of overlap area between the candidate hero rect (centered at
        // `center`, sized `footprint`) and every hazard rect. Cell-space units.
        private static float OverlapArea(Vector2 center, Vector2 footprint, IReadOnlyList<HazardRect> hazards)
        {
            if (hazards == null || hazards.Count == 0) return 0f;
            float halfX = footprint.x * 0.5f;
            float halfY = footprint.y * 0.5f;
            float hMinX = center.x - halfX;
            float hMaxX = center.x + halfX;
            float hMinY = center.y - halfY;
            float hMaxY = center.y + halfY;

            float total = 0f;
            for (int i = 0; i < hazards.Count; i++)
            {
                var h = hazards[i];
                float hzMinX = h.Anchor.x;
                float hzMaxX = h.Anchor.x + h.Footprint.x;
                float hzMinY = h.Anchor.y;
                float hzMaxY = h.Anchor.y + h.Footprint.y;

                float ox = Mathf.Min(hMaxX, hzMaxX) - Mathf.Max(hMinX, hzMinX);
                float oy = Mathf.Min(hMaxY, hzMaxY) - Mathf.Max(hMinY, hzMinY);
                if (ox > 0f && oy > 0f) total += ox * oy;
            }
            return total;
        }

        private static Vector2 Rotate(Vector2 v, float degrees)
        {
            float rad = degrees * Mathf.Deg2Rad;
            float c = Mathf.Cos(rad);
            float s = Mathf.Sin(rad);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }
    }
}
