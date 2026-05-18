using UnityEngine;

namespace BossJam.Enemies
{
    /// <summary>
    /// "Where does the AI think the boss is right now?"
    ///
    /// Combines reaction lag and linear extrapolation: the predictor reads the boss
    /// state from <see cref="ReactionTimeSeconds"/> ago, estimates velocity from the
    /// observation window ending at that point, and projects forward by the same lag.
    ///
    /// Behaviour:
    ///   • Boss moves predictably → extrapolation cancels the lag → prediction = current pos.
    ///   • Boss changes direction → velocity is stale → hero commits to the old direction
    ///     for ~ReactionTime, then catches up as new samples enter the velocity window.
    ///
    /// Owns its own <see cref="BossPerception"/> buffer. Callers feed observations via
    /// <see cref="Observe"/> and read estimates via <see cref="Predict"/>.
    /// </summary>
    public sealed class BossPredictor
    {
        private readonly BossPerception perception;

        public float ReactionTimeSeconds = 0.25f;
        public float VelocityWindowSeconds = 0.1f;
        // Optional cap on the extrapolation magnitude (cells). 0 = no cap. Useful so a
        // teleport or huge velocity spike doesn't send the predicted point off the map.
        public float MaxExtrapolationCells = 0f;

        public BossPredictor(float bufferSeconds = 1f)
        {
            perception = new BossPerception(bufferSeconds);
        }

        public void Observe(float time, Vector2 center) => perception.Push(time, center);

        public void Clear() => perception.Clear();

        /// <summary>
        /// Returns the predictor's best guess of the boss's current center.
        /// <paramref name="fallback"/> is used if the buffer is empty.
        /// </summary>
        public Vector2 Predict(float now, Vector2 fallback)
        {
            if (!perception.TryGetPerceived(now, ReactionTimeSeconds, out var pPast))
                return fallback;

            perception.TryGetVelocity(now, ReactionTimeSeconds, VelocityWindowSeconds, out var v);
            Vector2 extrapolation = v * ReactionTimeSeconds;

            if (MaxExtrapolationCells > 0f &&
                extrapolation.sqrMagnitude > MaxExtrapolationCells * MaxExtrapolationCells)
            {
                extrapolation = extrapolation.normalized * MaxExtrapolationCells;
            }

            return pPast + extrapolation;
        }
    }
}
