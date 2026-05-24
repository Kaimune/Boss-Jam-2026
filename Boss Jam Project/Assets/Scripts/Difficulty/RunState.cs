using System.Collections.Generic;
using UnityEngine;

namespace BossJam.Difficulty
{
    /// <summary>
    /// Persistent run state that survives scene reloads. DifficultyRuntime is
    /// destroyed every time the gameplay scene reloads between waves; the wave
    /// counter and applied tier list live here so they can be rehydrated.
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
        public List<Difficulty> appliedTiers = new();
        public string currentTierName = "Immortal";
        public Difficulty currentTierEntry;

        public string previousTierName = "Immortal";
        public Difficulty previousTierEntry;

        public bool IsMidRun => appliedTiers != null && appliedTiers.Count > 0;

        public void ResetForNewRun()
        {
            currentWaveIndex = 1;
            appliedTiers.Clear();
            currentTierName = "Immortal";
            currentTierEntry = null;
            previousTierName = "Immortal";
            previousTierEntry = null;
        }

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
