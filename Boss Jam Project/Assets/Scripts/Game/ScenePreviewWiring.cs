using BossJam.Cutscene;
using BossJam.Dialogue;
using BossJam.Difficulty;
using TMPro;
using UnityEngine;

namespace BossJam.Game
{
    /// <summary>
    /// Scene-side address book for the Scene State Preview editor tool. Holds
    /// refs to the same things the runtime UI scripts toggle — inner Panel
    /// children and CanvasGroup alphas — never the parent canvases (those
    /// must stay active for any UI to render).
    ///
    /// Pure data — no Update, no event subscriptions, no runtime behavior.
    /// Only used by ScenePreviewWindow (editor-only). Safe to leave in builds.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ScenePreviewWiring : MonoBehaviour
    {
        [Header("Toggleable panels (inner Panel children that runtime SetActives)")]
        [Tooltip("StartScreen's Panel child — the visible start-screen layout.")]
        public GameObject startPanel;
        [Tooltip("IntermediateScreen's Panel child — the difficulty card layout.")]
        public GameObject intermediatePanel;
        [Tooltip("DeathScreen's Panel child — kept hidden in every preview state " +
                 "(runtime forcibly hides DeathScreenUI; Death itself is just the fade).")]
        public GameObject deathPanel;
        [Tooltip("GameOver's Panel child — the 'You died' layout.")]
        public GameObject gameOverPanel;

        [Header("Gameplay HUD elements (hidden in every non-Playing preview)")]
        [Tooltip("Children of HUDCanvas that are part of the gameplay HUD — health " +
                 "bars, tier label, ability bar. Mirror HudVisibility's authored list.")]
        public GameObject[] gameplayHudElements;

        [Header("Per-state extras — shown only when previewing that state")]
        [Tooltip("Extra GameObjects to show when previewing Startup. Hidden in every other state.")]
        public GameObject[] extraStartup;
        [Tooltip("Extra GameObjects to show when previewing Narration. Hidden in every other state.")]
        public GameObject[] extraNarration;
        [Tooltip("Extra GameObjects to show when previewing Intermediate. Hidden in every other state.")]
        public GameObject[] extraIntermediate;
        [Tooltip("Extra GameObjects to show when previewing Dialogue. Hidden in every other state.")]
        public GameObject[] extraDialogue;
        [Tooltip("Extra GameObjects to show when previewing Death. Hidden in every other state.")]
        public GameObject[] extraDeath;
        [Tooltip("Extra GameObjects to show when previewing GameOver. Hidden in every other state.")]
        public GameObject[] extraGameOver;

        [Header("Alpha-toggled overlays (runtime hides via CanvasGroup.alpha = 0)")]
        [Tooltip("CanvasGroup on NarrationPanel — alpha=1 shows the narration overlay.")]
        public CanvasGroup narrationCanvasGroup;
        [Tooltip("CanvasGroup on DialogueCanvas — toggled to alpha=1 by DialogueController.PreviewLine.")]
        public CanvasGroup dialogueCanvasGroup;

        [Header("Sample-injection targets")]
        [Tooltip("TMP_Text inside NarrationPanel — previewer writes the first line here.")]
        public TMP_Text narrationCaption;
        [Tooltip("DialogueController on DialogueCanvas — previewer calls PreviewLine.")]
        public DialogueController dialogueController;

        [Header("Cutscene fade")]
        [Tooltip("FadeOverlay component (Image-based fade). Previewer toggles enabled + alpha.")]
        public FadeOverlay fadeOverlay;

        [Header("Sample inputs (designer-editable)")]
        public DifficultyProfile profile;
        [Tooltip("1-based index into profile.tiers — currently unused for the simple preview but reserved for richer tier-card injection later.")]
        [Min(1)] public int previewTierIndex = 3;
        [Tooltip("Name of a dialogue script under Resources/Dialogue/ (no extension).")]
        public string previewDialogueScriptName;
        [Min(0)] public int previewDialogueLineIndex = 0;
        [Tooltip("Name of a narration script under Resources/Narration/ (no extension).")]
        public string previewNarrationScriptName;

        private void OnValidate()
        {
#if UNITY_EDITOR
            var all = FindObjectsByType<ScenePreviewWiring>(FindObjectsSortMode.None);
            if (all != null && all.Length > 1)
                Debug.LogWarning($"{nameof(ScenePreviewWiring)}: multiple instances in scene ({all.Length}). " +
                                 "ScenePreviewWindow uses the first found — duplicates are ignored.", this);
#endif
        }
    }
}
