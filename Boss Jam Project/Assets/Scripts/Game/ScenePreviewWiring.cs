using BossJam.Cutscene;
using BossJam.Dialogue;
using BossJam.Difficulty;
using TMPro;
using UnityEngine;

namespace BossJam.Game
{
    /// <summary>
    /// Scene-side address book for the Scene State Preview editor tool. Holds
    /// refs to every UI module the previewer toggles, plus sample-input fields
    /// so designers can choose which tier / dialogue line / narration line gets
    /// injected.
    ///
    /// Pure data — no Update, no event subscriptions, no runtime behavior. Only
    /// used by ScenePreviewWindow (editor-only). Safe to leave in builds; the
    /// component does nothing at runtime.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ScenePreviewWiring : MonoBehaviour
    {
        [Header("UI roots — GameObject toggles")]
        public GameObject startScreen;
        public GameObject narrationRoot;
        public GameObject intermediateRoot;
        public GameObject dialogueRoot;
        public GameObject deathScreenRoot;
        public GameObject gameOverScreenRoot;
        public GameObject gameplayHUD;

        [Header("Letterbox + fade")]
        public RectTransform letterboxTop;
        public RectTransform letterboxBottom;
        public CanvasGroup fadeOverlay;

        [Header("Sample-injection targets")]
        [Tooltip("TMP_Text inside narrationRoot that the previewer writes a sample line into.")]
        public TMP_Text narrationCaption;
        [Tooltip("TierCardUI inside intermediateRoot — previewer calls PreviewRender on this.")]
        public TierCardUI tierCard;
        [Tooltip("DialogueController inside dialogueRoot — previewer calls PreviewLine on this.")]
        public DialogueController dialogueController;

        [Header("Sample inputs (designer-editable)")]
        public DifficultyProfile profile;
        [Tooltip("1-based index into profile.tiers used for the Intermediate preview.")]
        [Min(1)] public int previewTierIndex = 3;
        public DialogueScriptAsset previewDialogueScript;
        [Tooltip("Index into previewDialogueScript.lines used for the Dialogue preview.")]
        [Min(0)] public int previewDialogueLineIndex = 0;
        [Tooltip("Name of a narration script under Resources/Narration/ (no extension). " +
                 "Previewer loads it and uses the first line.")]
        public string previewNarrationScriptName;

        private void OnValidate()
        {
#if UNITY_EDITOR
            // Catch duplicates so the previewer doesn't silently pick the wrong one.
            var all = FindObjectsByType<ScenePreviewWiring>(FindObjectsSortMode.None);
            if (all != null && all.Length > 1)
                Debug.LogWarning($"{nameof(ScenePreviewWiring)}: multiple instances in scene ({all.Length}). " +
                                 "ScenePreviewWindow uses the first found — duplicates are ignored.", this);
#endif
        }
    }
}
