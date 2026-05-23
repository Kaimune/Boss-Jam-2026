using System.Collections;
using BossJam.Cutscene;
using BossJam.Game;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BossJam.UI
{
    /// <summary>
    /// Plain title screen shown while GameStateController is in Startup. Press
    /// SPACE → Begin() → transitions into Intermediate (the difficulty card).
    /// On mid-run scene reloads the title arrives under the outro's black
    /// FadeOverlay; this script fades the overlay out before arming input so
    /// the player isn't pressing into a black screen.
    ///
    /// Time is paused during Startup, so all waits use unscaled time.
    /// </summary>
    public class StartScreenUI : MonoBehaviour
    {
        [Header("UI refs")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text promptLabel;

        [Header("Scene refs")]
        [SerializeField] private FadeOverlay fadeOverlay;

        [Header("Copy")]
        [SerializeField] private string titleText = "Labyrinth";
        [SerializeField] private string promptText = "Press SPACE to play";

        [Header("Mid-run fade-in")]
        [Tooltip("Seconds to fade the outro's black overlay out when arriving mid-run.")]
        [SerializeField] private float fadeOutSeconds = 0.5f;

        private GameStateController controller;
        private bool inputArmed;

        private void Awake()
        {
            controller = GameStateController.Instance ?? FindFirstObjectByType<GameStateController>();
            // FadeOverlay lives in the scene, not in this prefab. Auto-resolve so
            // mid-run reloads don't strand us behind the outro's black overlay.
            if (fadeOverlay == null) fadeOverlay = FindFirstObjectByType<FadeOverlay>();

            if (controller == null || panelRoot == null)
            {
                Debug.LogWarning($"{nameof(StartScreenUI)}: missing reference; disabling.", this);
                enabled = false;
                return;
            }

            if (titleLabel != null) titleLabel.text = titleText;
            if (promptLabel != null) promptLabel.text = promptText;

            bool show = controller.State == GameState.Startup;
            panelRoot.SetActive(show);
            if (!show) { enabled = false; return; }

            StartCoroutine(RunIntro(GameSession.IsMidRun));
        }

        private void OnEnable()
        {
            if (controller != null) controller.StateChanged += OnStateChanged;
        }

        private void OnDisable()
        {
            if (controller != null) controller.StateChanged -= OnStateChanged;
        }

        private void OnStateChanged(GameState state)
        {
            if (panelRoot != null) panelRoot.SetActive(state == GameState.Startup);
        }

        private void Update()
        {
            if (!inputArmed || controller == null || controller.State != GameState.Startup) return;
            var kb = Keyboard.current;
            if (kb != null && kb.spaceKey.wasPressedThisFrame)
            {
                // Mid-run keeps applied debuffs; only a true fresh launch wipes RunState.
                if (!GameSession.IsMidRun) GameSession.StartNewRun();
                controller.Begin();
            }
        }

        private IEnumerator RunIntro(bool midRun)
        {
            inputArmed = false;
            if (midRun && fadeOverlay != null)
            {
                yield return fadeOverlay.FadeOut(fadeOutSeconds);
            }
            inputArmed = true;
        }
    }
}
