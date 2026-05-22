using BossJam.Difficulty;
using BossJam.Game;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BossJam.UI
{
    /// <summary>
    /// Full-screen overlay shown while GameStateController is in Death. Displays
    /// the current tier name (falling back to "Immortal" before any debuff has
    /// landed) and waits for Space to call GameStateController.Resume().
    ///
    /// The controller owns pause / boss respawn / state transition; this UI is
    /// purely display + one input. Wire panelRoot + tierLabel in the Inspector.
    /// </summary>
    public class DeathScreenUI : MonoBehaviour
    {
        [SerializeField] private DifficultyRuntime runtime;

        [Header("UI refs")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private TMP_Text tierLabel;
        [SerializeField] private TMP_Text tierDescriptionLabel;
        [SerializeField] private TMP_Text promptLabel;

        [Header("Copy")]
        [SerializeField] private string promptText = "Press SPACE to continue";

        private GameStateController controller;

        private void Awake()
        {
            controller = GameStateController.Instance ?? FindFirstObjectByType<GameStateController>();
            if (runtime == null) runtime = FindFirstObjectByType<DifficultyRuntime>();

            if (controller == null || panelRoot == null || tierLabel == null)
            {
                Debug.LogWarning($"{nameof(DeathScreenUI)}: missing reference; disabling.", this);
                enabled = false;
                return;
            }

            if (promptLabel != null) promptLabel.text = promptText;
            panelRoot.SetActive(controller.State == GameState.Death);
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
            switch (state)
            {
                case GameState.Death:
                    RefreshLabels();
                    panelRoot.SetActive(true);
                    break;
                case GameState.Playing:
                    panelRoot.SetActive(false);
                    break;
            }
        }

        private void RefreshLabels()
        {
            string tierName = runtime != null ? runtime.CurrentTierName : DifficultyRuntime.ImmortalTierName;
            var entry = runtime != null ? runtime.CurrentTierEntry : null;

            tierLabel.text = tierName;
            tierLabel.color = entry != null ? entry.tint : Color.white;

            if (tierDescriptionLabel != null)
                tierDescriptionLabel.text = entry != null ? entry.tierDescription : "";
        }

        private void Update()
        {
            if (controller == null || controller.State != GameState.Death) return;
            // Direct keyboard polling so this works at Time.timeScale = 0 and
            // doesn't fight the boss's Space-bound ult (boss is disabled here).
            var kb = Keyboard.current;
            if (kb != null && kb.spaceKey.wasPressedThisFrame) controller.Resume();
        }
    }
}
