#if UNITY_EDITOR
using System.Collections.Generic;
using BossJam.Cutscene;
using BossJam.Dialogue;
using BossJam.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BossJam.Editor.ScenePreview
{
    /// <summary>
    /// Editor window — opens via Tools/BossJam/Scene State Preview.
    /// Finds the active scene's ScenePreviewWiring and exposes a button per
    /// GameState that snaps the scene's UI to that state's composition.
    /// Companion to the GameState enum in BossJam.Game.GameStateController.
    /// </summary>
    public sealed class ScenePreviewWindow : EditorWindow
    {
        [MenuItem("Tools/BossJam/Scene State Preview")]
        public static void Open()
        {
            var w = GetWindow<ScenePreviewWindow>("Scene State Preview");
            w.minSize = new Vector2(280, 360);
        }

        // Per-session snapshot of original visibility, captured lazily on the
        // first Apply call after window-open / domain-reload / scene change.
        private class GameObjectSnap { public GameObject go; public bool active; }
        private class RectSnap { public RectTransform rect; public Vector2 sizeDelta; }

        private readonly List<GameObjectSnap> goSnaps = new();
        private readonly List<RectSnap> rectSnaps = new();
        private bool snapshotTaken;
        private string snapshotSceneName;

        private void OnGUI()
        {
            var wiring = FindWiring();
            if (wiring == null)
            {
                EditorGUILayout.HelpBox(
                    "No ScenePreviewWiring found in the active scene. Add a " +
                    "ScenePreviewWiring component to a GameObject (e.g. _SceneDebug/) " +
                    "and populate its refs.",
                    MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("Scene States", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            DrawStateButton("Startup", wiring, ApplyStartup);
            DrawStateButton("Narration", wiring, ApplyNarration);
            DrawStateButton("Intermediate", wiring, ApplyIntermediate);
            DrawStateButton("CutsceneIntro", wiring, ApplyCutsceneIntro);
            DrawStateButton("Dialogue", wiring, ApplyDialogue);
            DrawStateButton("Playing", wiring, ApplyPlaying);
            DrawStateButton("Death", wiring, ApplyDeath);
            DrawStateButton("GameOver", wiring, ApplyGameOver);

            EditorGUILayout.Space(12);
            if (GUILayout.Button("Restore Defaults", GUILayout.Height(28)))
                RestoreDefaults(wiring);

            EditorGUILayout.Space(8);
            EditorGUILayout.HelpBox(
                "Previewing modifies the scene and marks it dirty. Click " +
                "Restore Defaults (or Cmd-Z) before saving if you don't want " +
                "the preview state persisted.",
                MessageType.None);
        }

        private void DrawStateButton(string label, ScenePreviewWiring w, System.Action<ScenePreviewWiring> apply)
        {
            if (GUILayout.Button(label, GUILayout.Height(24)))
                apply(w);
        }

        private static ScenePreviewWiring FindWiring()
        {
            return FindFirstObjectByType<ScenePreviewWiring>(FindObjectsInactive.Include);
        }

        // ---------- Apply methods (one per GameState) ----------

        private void ApplyStartup(ScenePreviewWiring w)
        {
            EnsureSnapshot(w);
            SetActive(w.startScreen, true);
            SetActive(w.narrationRoot, false);
            SetActive(w.intermediateRoot, false);
            SetActive(w.dialogueRoot, false);
            SetActive(w.deathScreenRoot, false);
            SetActive(w.gameOverScreenRoot, false);
            SetActive(w.gameplayHUD, false);
            SetLetterboxActive(w, false);
            SetFadeAlpha(w.fadeOverlay, enabled: false, alpha: 0f);
            MarkSceneDirty();
        }

        private void ApplyNarration(ScenePreviewWiring w)
        {
            EnsureSnapshot(w);
            SetActive(w.startScreen, false);
            SetActive(w.narrationRoot, true);
            SetActive(w.intermediateRoot, false);
            SetActive(w.dialogueRoot, false);
            SetActive(w.deathScreenRoot, false);
            SetActive(w.gameOverScreenRoot, false);
            SetActive(w.gameplayHUD, false);
            SetLetterboxActive(w, false);
            SetFadeAlpha(w.fadeOverlay, enabled: true, alpha: 1f);

            if (w.narrationCaption != null)
            {
                string sample = LoadNarrationFirstLine(w.previewNarrationScriptName);
                w.narrationCaption.text = sample ?? "[no narration script]";
            }
            MarkSceneDirty();
        }

        private void ApplyIntermediate(ScenePreviewWiring w)
        {
            EnsureSnapshot(w);
            SetActive(w.startScreen, false);
            SetActive(w.narrationRoot, false);
            SetActive(w.intermediateRoot, true);
            SetActive(w.dialogueRoot, false);
            SetActive(w.deathScreenRoot, false);
            SetActive(w.gameOverScreenRoot, false);
            SetActive(w.gameplayHUD, false);
            SetLetterboxActive(w, false);
            SetFadeAlpha(w.fadeOverlay, enabled: false, alpha: 0f);

            if (w.tierCard != null && w.profile != null && w.profile.tiers != null
                && w.profile.tiers.Count > 0)
            {
                int idx = Mathf.Clamp(w.previewTierIndex - 1, 0, w.profile.tiers.Count - 1);
                var tier = w.profile.tiers[idx];
                if (tier != null) w.tierCard.PreviewRender(tier.tierName, tier.description);
                else w.tierCard.PreviewRender("[no tier]", "");
            }
            MarkSceneDirty();
        }

        private void ApplyCutsceneIntro(ScenePreviewWiring w)
        {
            EnsureSnapshot(w);
            SetActive(w.startScreen, false);
            SetActive(w.narrationRoot, false);
            SetActive(w.intermediateRoot, false);
            SetActive(w.dialogueRoot, false);
            SetActive(w.deathScreenRoot, false);
            SetActive(w.gameOverScreenRoot, false);
            SetActive(w.gameplayHUD, false);
            SetLetterboxActive(w, true);
            SetFadeAlpha(w.fadeOverlay, enabled: false, alpha: 0f);
            MarkSceneDirty();
        }

        private void ApplyDialogue(ScenePreviewWiring w)
        {
            EnsureSnapshot(w);
            SetActive(w.startScreen, false);
            SetActive(w.narrationRoot, false);
            SetActive(w.intermediateRoot, false);
            SetActive(w.dialogueRoot, true);
            SetActive(w.deathScreenRoot, false);
            SetActive(w.gameOverScreenRoot, false);
            SetActive(w.gameplayHUD, false);
            SetLetterboxActive(w, true);
            SetFadeAlpha(w.fadeOverlay, enabled: false, alpha: 0f);

            if (w.dialogueController != null && w.previewDialogueScript != null
                && w.previewDialogueScript.lines != null
                && w.previewDialogueScript.lines.Count > 0)
            {
                int idx = Mathf.Clamp(w.previewDialogueLineIndex, 0,
                                      w.previewDialogueScript.lines.Count - 1);
                w.dialogueController.PreviewLine(w.previewDialogueScript.lines[idx]);
            }
            MarkSceneDirty();
        }

        private void ApplyPlaying(ScenePreviewWiring w)
        {
            EnsureSnapshot(w);
            SetActive(w.startScreen, false);
            SetActive(w.narrationRoot, false);
            SetActive(w.intermediateRoot, false);
            SetActive(w.dialogueRoot, false);
            SetActive(w.deathScreenRoot, false);
            SetActive(w.gameOverScreenRoot, false);
            SetActive(w.gameplayHUD, true);
            SetLetterboxActive(w, false);
            SetFadeAlpha(w.fadeOverlay, enabled: false, alpha: 0f);
            MarkSceneDirty();
        }

        private void ApplyDeath(ScenePreviewWiring w)
        {
            EnsureSnapshot(w);
            SetActive(w.startScreen, false);
            SetActive(w.narrationRoot, false);
            SetActive(w.intermediateRoot, false);
            SetActive(w.dialogueRoot, false);
            SetActive(w.deathScreenRoot, true);
            SetActive(w.gameOverScreenRoot, false);
            SetActive(w.gameplayHUD, false);
            SetLetterboxActive(w, true);
            // Fade enabled, alpha left at scene-authored value (do not overwrite).
            if (w.fadeOverlay != null) w.fadeOverlay.gameObject.SetActive(true);
            MarkSceneDirty();
        }

        private void ApplyGameOver(ScenePreviewWiring w)
        {
            EnsureSnapshot(w);
            SetActive(w.startScreen, false);
            SetActive(w.narrationRoot, false);
            SetActive(w.intermediateRoot, false);
            SetActive(w.dialogueRoot, false);
            SetActive(w.deathScreenRoot, false);
            SetActive(w.gameOverScreenRoot, true);
            SetActive(w.gameplayHUD, false);
            SetLetterboxActive(w, true);
            if (w.fadeOverlay != null) w.fadeOverlay.gameObject.SetActive(true);
            MarkSceneDirty();
        }

        // ---------- Toggle helpers ----------

        private static void SetActive(GameObject go, bool active)
        {
            if (go != null) go.SetActive(active);
        }

        private static void SetLetterboxActive(ScenePreviewWiring w, bool active)
        {
            if (w.letterboxTop != null) w.letterboxTop.gameObject.SetActive(active);
            if (w.letterboxBottom != null) w.letterboxBottom.gameObject.SetActive(active);
        }

        private static void SetFadeAlpha(FadeOverlay fade, bool enabled, float alpha)
        {
            if (fade == null) return;
            fade.gameObject.SetActive(enabled);
            if (enabled) fade.SetAlpha(alpha);
        }

        private static string LoadNarrationFirstLine(string scriptName)
        {
            if (string.IsNullOrWhiteSpace(scriptName)) return null;
            var script = NarrationScriptLoader.Load(scriptName);
            if (script == null || script.lines == null || script.lines.Count == 0) return null;
            return script.lines[0];
        }

        // ---------- Snapshot / restore ----------

        private void EnsureSnapshot(ScenePreviewWiring w)
        {
            string activeScene = SceneManager.GetActiveScene().name;
            if (snapshotTaken && snapshotSceneName == activeScene) return;

            goSnaps.Clear();
            rectSnaps.Clear();

            void AddGo(GameObject go)
            {
                if (go != null) goSnaps.Add(new GameObjectSnap { go = go, active = go.activeSelf });
            }

            AddGo(w.startScreen);
            AddGo(w.narrationRoot);
            AddGo(w.intermediateRoot);
            AddGo(w.dialogueRoot);
            AddGo(w.deathScreenRoot);
            AddGo(w.gameOverScreenRoot);
            AddGo(w.gameplayHUD);
            if (w.letterboxTop != null) AddGo(w.letterboxTop.gameObject);
            if (w.letterboxBottom != null) AddGo(w.letterboxBottom.gameObject);
            if (w.fadeOverlay != null) AddGo(w.fadeOverlay.gameObject);

            if (w.letterboxTop != null)
                rectSnaps.Add(new RectSnap { rect = w.letterboxTop, sizeDelta = w.letterboxTop.sizeDelta });
            if (w.letterboxBottom != null)
                rectSnaps.Add(new RectSnap { rect = w.letterboxBottom, sizeDelta = w.letterboxBottom.sizeDelta });

            snapshotTaken = true;
            snapshotSceneName = activeScene;
        }

        private void RestoreDefaults(ScenePreviewWiring w)
        {
            if (!snapshotTaken)
            {
                Debug.Log("[ScenePreview] No snapshot to restore — capture happens on first Apply.");
                return;
            }
            foreach (var s in goSnaps) if (s.go != null) s.go.SetActive(s.active);
            foreach (var s in rectSnaps) if (s.rect != null) s.rect.sizeDelta = s.sizeDelta;
            MarkSceneDirty();
        }

        private static void MarkSceneDirty()
        {
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }
    }
}
#endif
