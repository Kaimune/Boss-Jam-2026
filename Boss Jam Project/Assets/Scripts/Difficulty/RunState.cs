using System.Collections.Generic;
using UnityEngine;

namespace BossJam.Difficulty
{
    /// <summary>
    /// Persistent run state that survives scene reloads. DifficultyRuntime is
    /// destroyed every time the gameplay scene reloads between waves; the wave
    /// counter and applied debuff list live here so they can be rehydrated.
    ///
    /// ScriptableObjects keep their values across SceneManager.LoadScene within
    /// a playmode session and reset to their serialized defaults when playmode
    /// exits — exactly the lifecycle we want for a single-session run.
    /// </summary>
    [CreateAssetMenu(fileName = "RunState", menuName = "BossJam/Run State")]
    public sealed class RunState : ScriptableObject
    {
        [Header("Persisted across scene reloads")]
        public int currentWaveIndex = 1;
        public List<DebuffEntry> appliedEntries = new();
        public string currentTierName = "Immortal";
        public DebuffEntry currentTierEntry;

        public bool IsMidRun => appliedEntries != null && appliedEntries.Count > 0;

        public void ResetForNewRun()
        {
            currentWaveIndex = 1;
            appliedEntries.Clear();
            currentTierName = "Immortal";
            currentTierEntry = null;
        }

        // Guard against Unity's "Enter Play Mode → Reload Domain" being
        // disabled. Without this, ScriptableObject state from the previous
        // playmode session bleeds into the next one and the start screen
        // gets skipped on a fresh play.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetAllOnSessionStart()
        {
#if UNITY_EDITOR
            var guids = UnityEditor.AssetDatabase.FindAssets($"t:{nameof(RunState)}");
            for (int i = 0; i < guids.Length; i++)
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[i]);
                var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<RunState>(path);
                if (asset != null) asset.ResetForNewRun();
            }
#endif
        }
    }
}
