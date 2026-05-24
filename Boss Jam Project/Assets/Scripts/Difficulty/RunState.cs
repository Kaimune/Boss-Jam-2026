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
        public string currentTierName = "";
        public Difficulty currentTierEntry;

        public string previousTierName = "";
        public Difficulty previousTierEntry;

        [Tooltip("Set true by AdvanceTier so the next scene load plays the new tier's " +
                 "narration once, then cleared. False on boss-death replays so the " +
                 "narration doesn't loop.")]
        public bool pendingTierNarration;

        [Header("Debug (persists across ResetForNewRun)")]
        [Tooltip("If > 0, the runtime pre-applies this many tiers at game start. " +
                 "0 = normal flow (auto-advance to Tier 1 on Begin). Set via Tools > BossJam > Debug > Start At Tier.")]
        public int debugStartingTier = 0;

        public bool IsMidRun => appliedTiers != null && appliedTiers.Count > 0;

        public void ResetForNewRun()
        {
            currentWaveIndex = 1;
            appliedTiers.Clear();
            currentTierName = "";
            currentTierEntry = null;
            previousTierName = "";
            previousTierEntry = null;
            pendingTierNarration = false;
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
