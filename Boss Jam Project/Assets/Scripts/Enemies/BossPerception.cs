using System.Collections.Generic;
using UnityEngine;

namespace BossJam.Enemies
{
    /// <summary>
    /// Tiny ring buffer of (time, boss-center) samples. Used to model AI reaction lag:
    /// the consumer pushes the real boss state every frame and reads back what the AI
    /// "saw" some configurable number of seconds ago. Owned per-AI so each unit has its
    /// own reaction time.
    /// </summary>
    public sealed class BossPerception
    {
        private struct Sample { public float t; public Vector2 c; }

        private readonly List<Sample> samples = new List<Sample>();
        private readonly float bufferSeconds;

        public BossPerception(float bufferSeconds)
        {
            this.bufferSeconds = Mathf.Max(0.1f, bufferSeconds);
        }

        public void Push(float time, Vector2 center)
        {
            samples.Add(new Sample { t = time, c = center });

            // Drop entries older than the buffer window.
            int drop = 0;
            while (drop < samples.Count && time - samples[drop].t > bufferSeconds) drop++;
            if (drop > 0) samples.RemoveRange(0, drop);
        }

        /// <summary>
        /// Returns the newest sample with timestamp ≤ (now - delaySeconds). If the
        /// buffer hasn't filled yet (no sample that old exists), returns the oldest
        /// sample we have — produces a gradual warm-up rather than a snap.
        /// </summary>
        public bool TryGetPerceived(float now, float delaySeconds, out Vector2 center)
        {
            if (samples.Count == 0) { center = Vector2.zero; return false; }

            float target = now - delaySeconds;
            for (int i = samples.Count - 1; i >= 0; i--)
            {
                if (samples[i].t <= target) { center = samples[i].c; return true; }
            }
            center = samples[0].c;
            return true;
        }

        /// <summary>
        /// Velocity estimate (cells/second) around time <c>now - aroundDelay</c>, computed
        /// from a sample <c>windowSeconds</c> earlier than that. Returns false if there
        /// isn't enough history to span the window yet.
        /// </summary>
        public bool TryGetVelocity(float now, float aroundDelay, float windowSeconds, out Vector2 velocity)
        {
            velocity = Vector2.zero;
            if (samples.Count < 2) return false;

            float newerT = now - aroundDelay;
            float olderT = newerT - Mathf.Max(0.001f, windowSeconds);

            int newerIdx = IndexAtOrBefore(newerT);
            if (newerIdx < 0) newerIdx = 0;

            int olderIdx = IndexAtOrBefore(olderT);
            if (olderIdx < 0) olderIdx = 0;

            var newer = samples[newerIdx];
            var older = samples[olderIdx];
            float dt = newer.t - older.t;
            if (dt < 0.001f) return false;

            velocity = (newer.c - older.c) / dt;
            return true;
        }

        public void Clear() => samples.Clear();

        // Newest sample whose timestamp is ≤ t. -1 if none. Linear scan from the tail.
        private int IndexAtOrBefore(float t)
        {
            for (int i = samples.Count - 1; i >= 0; i--)
                if (samples[i].t <= t) return i;
            return -1;
        }
    }
}
