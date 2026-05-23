using BossJam.Difficulty;
using BossJam.Game;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BossJam.UI
{
    /// <summary>
    /// Full-screen overlay shown while GameStateController is in GameOver
    /// (boss killed by hero). Displays the tier the player died on and waits
    /// for Space to restart at the same tier.
    ///
    /// Pause / boss respawn / state transition are owned by the controller;
    /// this UI is purely display + one input.
    /// </summary>
    public class GameOverScreenUI : MonoBehaviour
    {
        [SerializeField] private DifficultyRuntime runtime;

        [Header("UI refs")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private TMP_Text tierLabel;
        [SerializeField] private TMP_Text tierDescriptionLabel;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text promptLabel;

        [Header("Copy")]
        [SerializeField] private string titleText = "You died";
        [SerializeField] private string promptText = "Press SPACE to restart this tier";

        private GameStateController controller;
        private BossJam.Cutscene.OutroDirector outroDirector;

        private void Awake()
        {
            controller = GameStateController.Instance ?? FindFirstObjectByType<GameStateController>();
            outroDirector = FindFirstObjectByType<BossJam.Cutscene.OutroDirector>();
            if (runtime == null) runtime = FindFirstObjectByType<DifficultyRuntime>();

            if (controller == null || panelRoot == null)
            {
                Debug.LogWarning($"{nameof(GameOverScreenUI)}: missing reference; disabling.", this);
                enabled = false;
                return;
            }

            if (titleLabel != null) titleLabel.text = titleText;
            if (promptLabel != null) promptLabel.text = promptText;
            panelRoot.SetActive(false);
        }

        private void OnEnable()
        {
            if (controller != null) controller.StateChanged += OnStateChanged;
            if (outroDirector != null) outroDirector.OutroComplete += OnOutroComplete;
        }

        private void OnDisable()
        {
            if (controller != null) controller.StateChanged -= OnStateChanged;
            if (outroDirector != null) outroDirector.OutroComplete -= OnOutroComplete;
        }

        private void OnStateChanged(GameState state)
        {
            // Hide on every other state. The cutscene must finish before we show the panel.
            if (state != GameState.GameOver && panelRoot != null) panelRoot.SetActive(false);
        }

        private void OnOutroComplete()
        {
            if (controller == null || controller.State != GameState.GameOver) return;
            RefreshLabels();
            if (panelRoot != null) panelRoot.SetActive(true);
        }

        private void RefreshLabels()
        {
            // Show the tier the player died on (their high-water mark).
            string tierName = runtime != null ? runtime.CurrentTierName : DifficultyRuntime.ImmortalTierName;
            var entry = runtime != null ? runtime.CurrentTierEntry : null;

            if (tierLabel != null)
            {
                tierLabel.text = tierName;
                tierLabel.color = entry != null ? entry.tint : Color.white;
            }
            if (tierDescriptionLabel != null)
                tierDescriptionLabel.text = entry != null ? entry.tierDescription : "";
        }

        private void Update()
        {
            if (controller == null || controller.State != GameState.GameOver) return;
            var kb = Keyboard.current;
            if (kb != null && kb.spaceKey.wasPressedThisFrame) controller.Resume();
        }
    }
}
