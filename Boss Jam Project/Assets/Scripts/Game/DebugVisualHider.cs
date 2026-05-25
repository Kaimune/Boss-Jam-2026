using BossJam.Difficulty;
using UnityEngine;

namespace BossJam.Game
{
    /// <summary>
    /// Shared helper for the showDebugVisuals toggle on RunState. Call
    /// <see cref="ApplyTo"/> on a spawned GameObject (attack hitbox, telegraph,
    /// future debug-only props) to silently hide its MeshRenderers when debug
    /// visuals are off. Cheap: one FindFirstObjectByType per call site at
    /// spawn time, then a single GetComponentsInChildren walk.
    /// </summary>
    public static class DebugVisualHider
    {
        public static void ApplyTo(GameObject go)
        {
            if (go == null) return;
            var rt = Object.FindFirstObjectByType<DifficultyRuntime>(FindObjectsInactive.Include);
            if (rt == null || rt.State == null || rt.State.showDebugVisuals) return;
            foreach (var mr in go.GetComponentsInChildren<MeshRenderer>(true))
                mr.enabled = false;
        }
    }
}
