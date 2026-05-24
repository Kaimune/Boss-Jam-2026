#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BossJam.Difficulty.Editor
{
    /// <summary>
    /// Editor-only debug menu — fast-forwards the persistent RunState so
    /// playmode starts at a chosen tier. Find the RunState + DifficultyProfile
    /// in the project, then "apply" each tier up to (and including) the chosen
    /// one by pre-populating appliedTiers + currentTier* fields.
    ///
    /// Effects are NOT applied here (no DifficultyRuntime exists at edit
    /// time). When playmode starts, DifficultyRuntime.Awake replays the
    /// LATEST applied tier's effects via the existing rehydration path —
    /// reset-on-advance semantics already give you absolute state.
    /// </summary>
    public static class DifficultyDebugMenu
    {
        private const string MenuRoot = "Tools/BossJam/Debug/Start At Tier/";

        [MenuItem(MenuRoot + "Immortal (no tier)")]    public static void Set0() => SetStartingTier(0);
        [MenuItem(MenuRoot + "1 — Impossible")]        public static void Set1() => SetStartingTier(1);
        [MenuItem(MenuRoot + "2 — Very Hard")]         public static void Set2() => SetStartingTier(2);
        [MenuItem(MenuRoot + "3 — Hard")]              public static void Set3() => SetStartingTier(3);
        [MenuItem(MenuRoot + "4 — Medium")]            public static void Set4() => SetStartingTier(4);
        [MenuItem(MenuRoot + "5 — Easy")]              public static void Set5() => SetStartingTier(5);
        [MenuItem(MenuRoot + "6 — Beginner")]          public static void Set6() => SetStartingTier(6);
        [MenuItem(MenuRoot + "7 — Tutorial")]          public static void Set7() => SetStartingTier(7);
        [MenuItem(MenuRoot + "8 — Error.")]            public static void Set8() => SetStartingTier(8);

        [MenuItem("Tools/BossJam/Debug/Reset RunState")]
        public static void ResetRunState() => SetStartingTier(0);

        /// <summary>
        /// Tier index is 1-based (Impossible = 1, Error. = 8). 0 = Immortal
        /// (no tier applied, fresh run).
        /// </summary>
        private static void SetStartingTier(int tierIndex)
        {
            var runStateGuids = AssetDatabase.FindAssets("t:RunState");
            if (runStateGuids.Length == 0)
            {
                Debug.LogError("[BossJam] No RunState asset found.");
                return;
            }
            var profileGuids = AssetDatabase.FindAssets("t:DifficultyProfile");
            if (profileGuids.Length == 0)
            {
                Debug.LogError("[BossJam] No DifficultyProfile asset found.");
                return;
            }

            var runState = AssetDatabase.LoadAssetAtPath<RunState>(
                AssetDatabase.GUIDToAssetPath(runStateGuids[0]));
            var profile = AssetDatabase.LoadAssetAtPath<DifficultyProfile>(
                AssetDatabase.GUIDToAssetPath(profileGuids[0]));

            if (runState == null || profile == null)
            {
                Debug.LogError("[BossJam] Failed to load RunState or DifficultyProfile.");
                return;
            }

            runState.ResetForNewRun();

            if (tierIndex < 0) tierIndex = 0;
            if (tierIndex > profile.tiers.Count) tierIndex = profile.tiers.Count;

            for (int i = 0; i < tierIndex; i++)
            {
                var tier = profile.tiers[i];
                if (tier == null) continue;

                // Mirror DifficultyRuntime.AdvanceTier()'s persistent-state
                // mutations (without applying effects — those run at playmode
                // start via the runtime's Awake rehydration).
                runState.previousTierName = runState.currentTierName;
                runState.previousTierEntry = runState.currentTierEntry;
                runState.appliedTiers.Add(tier);

                if (!string.IsNullOrEmpty(tier.tierName) && tier.tierName != runState.currentTierName)
                {
                    runState.currentTierName = tier.tierName;
                    runState.currentTierEntry = tier;
                }
                runState.currentWaveIndex++;
            }

            EditorUtility.SetDirty(runState);
            AssetDatabase.SaveAssets();

            string label = tierIndex == 0
                ? "Immortal (no tiers)"
                : $"tier {tierIndex} — '{runState.currentTierName}'";
            Debug.Log($"[BossJam] RunState set to start at {label}. " +
                      $"appliedTiers.Count = {runState.appliedTiers.Count}, " +
                      $"currentWaveIndex = {runState.currentWaveIndex}. " +
                      $"Enter playmode to run from this tier.");
        }
    }
}
#endif
