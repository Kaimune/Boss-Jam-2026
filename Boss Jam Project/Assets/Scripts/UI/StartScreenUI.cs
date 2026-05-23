using BossJam.Game;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BossJam.UI
{
    /// <summary>
    /// Full-screen overlay shown while GameStateController is in Startup.
    /// Time is paused and the boss is disabled until Space is pressed, which
    /// calls Begin() and transitions to Playing. Disappears once the run has
    /// started and never returns this session.
    /// </summary>
    public class StartScreenUI : MonoBehaviour
    {
        [Header("UI refs")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text promptLabel;

        [Header("Copy")]
        [SerializeField] private string titleText = "Boss Jam";
        [SerializeField] private string promptText = "Press SPACE to play";

        private GameStateController controller;

        private void Awake()
        {
            controller = GameStateController.Instance ?? FindFirstObjectByType<GameStateController>();

            if (controller == null || panelRoot == null)
            {
                Debug.LogWarning($"{nameof(StartScreenUI)}: missing reference; disabling.", this);
                enabled = false;
                return;
            }

            if (titleLabel != null) titleLabel.text = titleText;
            if (promptLabel != null) promptLabel.text = promptText;

            // Mid-run scene reloads land here with RunState carrying applied
            // debuffs from prior waves. Skip the start screen entirely — the
            // controller will auto-Begin on its own Start.
            bool showStartScreen = controller.State == GameState.Startup && !GameSession.IsMidRun;
            panelRoot.SetActive(showStartScreen);
            if (!showStartScreen) enabled = false;
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
            panelRoot.SetActive(state == GameState.Startup);
        }

        private void Update()
        {
            if (controller == null || controller.State != GameState.Startup) return;
            var kb = Keyboard.current;
            if (kb != null && kb.spaceKey.wasPressedThisFrame)
            {
                GameSession.StartNewRun();
                controller.Begin();
            }
        }
    }
}
