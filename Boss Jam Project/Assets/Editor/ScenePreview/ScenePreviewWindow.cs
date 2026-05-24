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
    /// Snaps the active scene's UI to a chosen GameState's visible composition.
    /// Only touches what the runtime toggles: inner Panel children + CanvasGroup
    /// alphas + the fade overlay. Never parent canvases (those must stay active).
    /// </summary>
    public sealed class ScenePreviewWindow : EditorWindow
    {
        [MenuItem("Tools/BossJam/Scene State Preview")]
        public static void Open()
        {
            var w = GetWindow<ScenePreviewWindow>("Scene State Preview");
            w.minSize = new Vector2(260, 320);
        }

        // Per-session snapshot of original visibility. Captured lazily on first Apply.
        private class GameObjectSnap { public GameObject go; public bool active; }
        private class CanvasGroupSnap { public CanvasGroup group; public float alpha; }
        private readonly List<GameObjectSnap> goSnaps = new();
        private readonly List<CanvasGroupSnap> cgSnaps = new();
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
            DrawStateButton("Dialogue", wiring, ApplyDialogue);
            DrawStateButton("Playing", wiring, ApplyPlaying);
            DrawStateButton("Death", wiring, ApplyDeath);
            DrawStateButton("GameOver", wiring, ApplyGameOver);

            EditorGUILayout.Space(12);
            if (GUILayout.Button("Restore Defaults", GUILayout.Height(28)))
                RestoreDefaults();

            EditorGUILayout.Space(8);
            EditorGUILayout.HelpBox(
                "Previewing modifies the scene and marks it dirty. Click " +
                "Restore Defaults (or Cmd-Z) BEFORE saving or entering Play " +
                "mode — otherwise the previewed state persists.",
                MessageType.Warning);
        }

        private void DrawStateButton(string label, ScenePreviewWiring w, System.Action<ScenePreviewWiring> apply)
        {
            if (GUILayout.Button(label, GUILayout.Height(24))) apply(w);
        }

        private static ScenePreviewWiring FindWiring()
        {
            return FindFirstObjectByType<ScenePreviewWiring>(FindObjectsInactive.Include);
        }

        // ---------- Apply methods ----------

        private void ApplyStartup(ScenePreviewWiring w)
        {
            EnsureSnapshot(w);
            HideAllPanels(w);
            HideGameplayHud(w);
            HideAllExtras(w);
            SetActive(w.startPanel, true);
            ShowArray(w.extraStartup);
            SetCanvasGroupAlpha(w.narrationCanvasGroup, 0f);
            SetCanvasGroupAlpha(w.dialogueCanvasGroup, 0f);
            SetFadeActive(w.fadeOverlay, false);
            MarkSceneDirty();
        }

        private void ApplyNarration(ScenePreviewWiring w)
        {
            EnsureSnapshot(w);
            HideAllPanels(w);
            HideGameplayHud(w);
            HideAllExtras(w);
            ShowArray(w.extraNarration);
            SetCanvasGroupAlpha(w.narrationCanvasGroup, 1f);
            SetCanvasGroupAlpha(w.dialogueCanvasGroup, 0f);
            SetFadeActive(w.fadeOverlay, true, 1f);

            // Only overwrite the body if a script loads cleanly; otherwise
            // leave whatever's authored in the scene (matches how Intermediate /
            // GameOver behave — show authored content as the default).
            if (w.narrationCaption != null)
            {
                string sample = LoadNarrationFirstLine(w.previewNarrationScriptName);
                if (!string.IsNullOrEmpty(sample)) w.narrationCaption.text = sample;
            }
            MarkSceneDirty();
        }

        private void ApplyIntermediate(ScenePreviewWiring w)
        {
            EnsureSnapshot(w);
            HideAllPanels(w);
            HideGameplayHud(w);
            HideAllExtras(w);
            SetActive(w.intermediatePanel, true);
            ShowArray(w.extraIntermediate);
            SetCanvasGroupAlpha(w.narrationCanvasGroup, 0f);
            SetCanvasGroupAlpha(w.dialogueCanvasGroup, 0f);
            SetFadeActive(w.fadeOverlay, false);
            MarkSceneDirty();
        }

        private void ApplyDialogue(ScenePreviewWiring w)
        {
            EnsureSnapshot(w);
            HideAllPanels(w);
            HideGameplayHud(w);
            HideAllExtras(w);
            ShowArray(w.extraDialogue);
            SetCanvasGroupAlpha(w.narrationCanvasGroup, 0f);
            SetFadeActive(w.fadeOverlay, false);

            if (w.dialogueController != null
                && !string.IsNullOrWhiteSpace(w.previewDialogueScriptName))
            {
                var asset = DialogueScriptLoader.Load(w.previewDialogueScriptName);
                if (asset != null && asset.lines != null && asset.lines.Count > 0)
                {
                    int idx = Mathf.Clamp(w.previewDialogueLineIndex, 0, asset.lines.Count - 1);
                    w.dialogueController.PreviewLine(asset.lines[idx]);
                }
            }
            else
            {
                SetCanvasGroupAlpha(w.dialogueCanvasGroup, 1f);
            }
            MarkSceneDirty();
        }

        private void ApplyPlaying(ScenePreviewWiring w)
        {
            // Playing state = gameplay HUD visible, everything else hidden.
            EnsureSnapshot(w);
            HideAllPanels(w);
            HideAllExtras(w);
            ShowArray(w.gameplayHudElements);
            ShowArray(w.extraPlaying);
            SetCanvasGroupAlpha(w.narrationCanvasGroup, 0f);
            SetCanvasGroupAlpha(w.dialogueCanvasGroup, 0f);
            SetFadeActive(w.fadeOverlay, false);
            MarkSceneDirty();
        }

        private void ApplyDeath(ScenePreviewWiring w)
        {
            // Death state in runtime = OutroDirector fade-to-black; no panel.
            EnsureSnapshot(w);
            HideAllPanels(w);
            HideGameplayHud(w);
            HideAllExtras(w);
            ShowArray(w.extraDeath);
            SetCanvasGroupAlpha(w.narrationCanvasGroup, 0f);
            SetCanvasGroupAlpha(w.dialogueCanvasGroup, 0f);
            SetFadeActive(w.fadeOverlay, true, 1f);
            MarkSceneDirty();
        }

        private void ApplyGameOver(ScenePreviewWiring w)
        {
            // GameOver state in runtime = fade fully black + GameOver panel on top.
            EnsureSnapshot(w);
            HideAllPanels(w);
            HideGameplayHud(w);
            HideAllExtras(w);
            SetActive(w.gameOverPanel, true);
            ShowArray(w.extraGameOver);
            SetCanvasGroupAlpha(w.narrationCanvasGroup, 0f);
            SetCanvasGroupAlpha(w.dialogueCanvasGroup, 0f);
            SetFadeActive(w.fadeOverlay, true, 1f);
            MarkSceneDirty();
        }

        private static void HideAllPanels(ScenePreviewWiring w)
        {
            SetActive(w.startPanel, false);
            SetActive(w.intermediatePanel, false);
            SetActive(w.deathPanel, false);
            SetActive(w.gameOverPanel, false);
        }

        private static void HideGameplayHud(ScenePreviewWiring w)
        {
            HideArray(w.gameplayHudElements);
        }

        private static void HideAllExtras(ScenePreviewWiring w)
        {
            HideArray(w.extraStartup);
            HideArray(w.extraNarration);
            HideArray(w.extraIntermediate);
            HideArray(w.extraDialogue);
            HideArray(w.extraPlaying);
            HideArray(w.extraDeath);
            HideArray(w.extraGameOver);
        }

        private static void HideArray(GameObject[] arr)
        {
            if (arr == null) return;
            for (int i = 0; i < arr.Length; i++) SetActive(arr[i], false);
        }

        private static void ShowArray(GameObject[] arr)
        {
            if (arr == null) return;
            for (int i = 0; i < arr.Length; i++) SetActive(arr[i], true);
        }

        // ---------- Helpers ----------

        private static void SetActive(GameObject go, bool active)
        {
            if (go != null) go.SetActive(active);
        }

        private static void SetCanvasGroupAlpha(CanvasGroup cg, float alpha)
        {
            if (cg != null) cg.alpha = alpha;
        }

        private static void SetFadeActive(FadeOverlay fade, bool active, float alpha = 0f)
        {
            if (fade == null) return;
            fade.gameObject.SetActive(active);
            if (active) fade.SetAlpha(alpha);
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
            cgSnaps.Clear();

            void AddGo(GameObject go)
            {
                if (go != null) goSnaps.Add(new GameObjectSnap { go = go, active = go.activeSelf });
            }
            void AddCg(CanvasGroup cg)
            {
                if (cg != null) cgSnaps.Add(new CanvasGroupSnap { group = cg, alpha = cg.alpha });
            }

            AddGo(w.startPanel);
            AddGo(w.intermediatePanel);
            AddGo(w.deathPanel);
            AddGo(w.gameOverPanel);
            if (w.fadeOverlay != null) AddGo(w.fadeOverlay.gameObject);
            SnapArray(w.gameplayHudElements);
            SnapArray(w.extraStartup);
            SnapArray(w.extraNarration);
            SnapArray(w.extraIntermediate);
            SnapArray(w.extraDialogue);
            SnapArray(w.extraPlaying);
            SnapArray(w.extraDeath);
            SnapArray(w.extraGameOver);

            AddCg(w.narrationCanvasGroup);
            AddCg(w.dialogueCanvasGroup);

            void SnapArray(GameObject[] arr)
            {
                if (arr == null) return;
                for (int i = 0; i < arr.Length; i++) AddGo(arr[i]);
            }

            snapshotTaken = true;
            snapshotSceneName = activeScene;
        }

        private void RestoreDefaults()
        {
            if (!snapshotTaken)
            {
                Debug.Log("[ScenePreview] No snapshot to restore — capture happens on first Apply.");
                return;
            }
            foreach (var s in goSnaps) if (s.go != null) s.go.SetActive(s.active);
            foreach (var s in cgSnaps) if (s.group != null) s.group.alpha = s.alpha;
            MarkSceneDirty();
        }

        private static void MarkSceneDirty()
        {
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }
    }
}
#endif
