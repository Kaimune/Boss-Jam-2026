using BossJam.Difficulty;
using UnityEngine;

namespace BossJam.Game
{
    /// <summary>
    /// Static facade over the scene's RunState. Used by the start screen to
    /// clear the run before pressing play, and by the state controller to
    /// decide whether a fresh scene load should auto-Begin (mid-run wave
    /// transition) or wait for input (true session start).
    ///
    /// The actual run data lives on the RunState ScriptableObject referenced
    /// by DifficultyRuntime in the scene.
    /// </summary>
    public static class GameSession
    {
        public static RunState Current
        {
            get
            {
                var runtime = Object.FindFirstObjectByType<DifficultyRuntime>();
                return runtime != null ? runtime.State : null;
            }
        }

        public static bool IsMidRun => Current != null && Current.IsMidRun;

        public static void StartNewRun()
        {
            var s = Current;
            if (s != null) s.ResetForNewRun();
        }
    }
}
