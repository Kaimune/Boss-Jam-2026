using System.Collections;
using UnityEngine;

namespace BossJam.Game
{
    /// <summary>
    /// Briefly pins <c>Time.timeScale = 0</c> for a configurable window so a
    /// hit reads with weight — the textbook action-game "thunk" frame.
    /// Scene-scoped singleton; call <see cref="Freeze"/> from anywhere.
    ///
    /// Behaviour:
    /// - Single coroutine. A second Freeze() call while one is in flight
    ///   restarts the freeze rather than stacking — chained hits stay snappy
    ///   without the timescale spiraling.
    /// - Only fires when <see cref="GameStateController.State"/> is Playing.
    ///   Dialogue/Intermediate already pause via timescale; piling another
    ///   freeze on top would corrupt their restore. Death/GameOver have
    ///   their own pacing.
    /// - Uses <c>WaitForSecondsRealtime</c> so the freeze window is exactly
    ///   the configured length regardless of timescale.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HitStopController : MonoBehaviour
    {
        public static HitStopController Instance { get; private set; }

        [Tooltip("Default freeze duration in real seconds. Tune for feel — 30–80ms is the action-game sweet spot.")]
        [SerializeField, Min(0f)] private float hitStopSeconds = 0.05f;

        private Coroutine routine;
        private float savedTimeScale = 1f;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                // Defensive restore: if we're destroyed mid-freeze (scene reload
                // during a hit-stop), don't leak timeScale=0 into the next scene.
                if (routine != null) Time.timeScale = savedTimeScale;
                Instance = null;
            }
        }

        /// <summary>Static convenience. No-ops if no controller exists in scene.</summary>
        public static void Freeze(float? secondsOverride = null)
        {
            Instance?.FreezeInstance(secondsOverride);
        }

        private void FreezeInstance(float? secondsOverride)
        {
            var gs = GameStateController.Instance;
            if (gs == null || gs.State != GameState.Playing) return;

            float seconds = secondsOverride.GetValueOrDefault(hitStopSeconds);
            if (seconds <= 0f) return;

            if (routine != null)
            {
                // Restart in place — current freeze hadn't restored yet, so
                // timeScale is already 0. Keep savedTimeScale from the first
                // entry; don't re-cache the 0 we set ourselves.
                StopCoroutine(routine);
            }
            else
            {
                savedTimeScale = Time.timeScale;
            }

            routine = StartCoroutine(FreezeRoutine(seconds));
        }

        private IEnumerator FreezeRoutine(float seconds)
        {
            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(seconds);
            Time.timeScale = savedTimeScale;
            routine = null;
        }
    }
}
