using BossJam.GridSystem;
using UnityEngine;

namespace BossJam.Enemies
{
    /// <summary>
    /// Pure-function steering for a kite AI. Picks a target point at <c>preferredRadius</c>
    /// from the boss along the boss→hero axis, rotating around the boss to find an
    /// in-bounds spot if the natural ray exits the grid. Returns a unit direction the
    /// hero should walk to reach that target.
    /// </summary>
    public static class HeroKiteSteering
    {
        // Resolution of the in-bounds search when the natural kite point is off-grid.
        private const int SearchSteps = 36;
        private const float StepDegrees = 360f / SearchSteps;

        public struct Result
        {
            public Vector2 Direction;     // unit vector toward target (Vector2.zero if at target)
            public Vector2 TargetPoint;   // cell-space waypoint (hero center)
            public bool ValidTargetFound; // false if no in-bounds point on the radius circle was found
        }

        public static Result Solve(
            Vector2 heroCenter,
            Vector2 bossCenter,
            float preferredRadius,
            BossGrid grid,
            Vector2 heroFootprint,
            int orbitSign)
        {
            Vector2 fromBoss = heroCenter - bossCenter;
            float dist = fromBoss.magnitude;
            Vector2 outDir = dist > 0.0001f ? fromBoss / dist : new Vector2(1f, 0f);

            // Preferred kite point: boss + R in the boss→hero direction.
            Vector2 target = bossCenter + outDir * preferredRadius;
            bool valid = IsValidCenter(target, grid, heroFootprint);

            // If off-grid, rotate around the boss in the orbit direction until a valid
            // point on the radius-R circle is found.
            if (!valid)
            {
                int sign = orbitSign >= 0 ? 1 : -1;
                for (int i = 1; i <= SearchSteps; i++)
                {
                    Vector2 rotated = Rotate(outDir, StepDegrees * i * sign);
                    Vector2 candidate = bossCenter + rotated * preferredRadius;
                    if (IsValidCenter(candidate, grid, heroFootprint))
                    {
                        target = candidate;
                        valid = true;
                        break;
                    }
                }
            }

            Vector2 toTarget = target - heroCenter;
            Vector2 dir = toTarget.sqrMagnitude < 0.01f ? Vector2.zero : toTarget.normalized;
            return new Result { Direction = dir, TargetPoint = target, ValidTargetFound = valid };
        }

        private static bool IsValidCenter(Vector2 center, BossGrid grid, Vector2 footprint)
        {
            Vector2 anchor = center - footprint * 0.5f;
            return grid != null && grid.InBounds(anchor, footprint);
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
