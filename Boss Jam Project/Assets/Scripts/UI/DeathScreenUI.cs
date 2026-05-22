using BossJam.Difficulty;
using BossJam.Game;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BossJam.UI
{
    /// <summary>
    /// Full-screen overlay shown while GameStateController is in Death. Death
    /// here means a hero was killed and a new tier is about to be applied —
    /// the panel previews the upcoming tier (from runtime.NextPreview) so the
    /// player sees what's coming before pressing Space to continue.
    ///
    /// The controller owns pause / debuff apply / state transition; this UI
    /// is purely display + one input. Wire panelRoot + tierLabel in the
    /// Inspector.
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
            // Show the upcoming tier — the next debuff in line that Resume()
            // will apply. Falls back to the current tier name if there is no
            // queued next (curve exhausted).
            var next = runtime != null ? runtime.NextPreview : null;
            string nextName = next != null && !string.IsNullOrEmpty(next.tierName)
                ? next.tierName
                : (runtime != null ? runtime.CurrentTierName : DifficultyRuntime.ImmortalTierName);

            tierLabel.text = nextName;
            tierLabel.color = next != null ? next.tint : Color.white;

            if (tierDescriptionLabel != null)
                tierDescriptionLabel.text = next != null ? next.tierDescription : "";
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
