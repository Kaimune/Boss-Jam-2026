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

        [Tooltip("One-shot: skip the intro cutscene + pre-fight dialogue on the next scene load, " +
                 "jump straight to Playing, and play the respawn_reload dialogue. Set by the hero " +
                 "respawn warning / boss save-scum flow; consumed by GameStateController.Begin.")]
        public bool skipNextIntro;

        [Tooltip("True while the hero's once-per-tier save-scum revival is still available. " +
                 "Consumed when the boss dies and the save fires; reset to true on each tier-apply.")]
        public bool saveScumAvailable = true;

        [Header("Debug (persists across ResetForNewRun)")]
        [Tooltip("If > 0, the runtime pre-applies this many tiers at game start. " +
                 "0 = normal flow (auto-advance to Tier 1 on Begin). Set via Tools > BossJam > Debug > Start At Tier.")]
        public int debugStartingTier = 0;

        [Tooltip("Master toggle for in-engine debug visuals — hero kite line/dot (green) and active-frame attack hitbox mesh (yellow/orange). Untick for clean play / screenshots; the attack telegraph (red) and gameplay logic are unaffected.")]
        public bool showDebugVisuals = true;

        [Tooltip("Enables the DebugBossHp dev keys: P = instakill the hero, O = drop the hero to 1 HP. Untick to disable the bindings entirely.")]
        public bool debugHeroHpKeys = true;

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
            skipNextIntro = false;
            saveScumAvailable = true;
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
