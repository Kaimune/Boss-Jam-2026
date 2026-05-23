using BossJam.Game;
using UnityEngine;

namespace BossJam.UI
{
    /// <summary>
    /// Hides the gameplay HUD widgets (health bars, tier label) whenever the
    /// game is not in Playing — covers Startup, Dialogue, Death, GameOver.
    /// Lives on HUDCanvas so it shares lifetime with the HUD it manages, but
    /// only toggles the listed gameplay-only children. Screen panels
    /// (StartScreen / DeathScreen / GameOver) are owned by their own
    /// controllers — leaving the Canvas itself enabled lets those panels
    /// render during non-Playing states.
    /// </summary>
    public sealed class HudVisibility : MonoBehaviour
    {
        [Tooltip("Children to hide whenever state != Playing. Leave empty to auto-resolve common names " +
                 "(BossHealthBar / HeroHealthBar / TierLabel).")]
        [SerializeField] private GameObject[] gameplayHudChildren;

        private GameStateController controller;

        private void Awake()
        {
            controller = FindFirstObjectByType<GameStateController>();
            if (gameplayHudChildren == null || gameplayHudChildren.Length == 0)
                AutoResolveChildren();
        }

        private void OnEnable()
        {
            if (controller != null)
            {
                controller.StateChanged += Apply;
                Apply(controller.State);
            }
        }

        private void OnDisable()
        {
            if (controller != null) controller.StateChanged -= Apply;
        }

        private void Apply(GameState state)
        {
            bool show = state == GameState.Playing;
            if (gameplayHudChildren == null) return;
            for (int i = 0; i < gameplayHudChildren.Length; i++)
            {
                if (gameplayHudChildren[i] != null)
                    gameplayHudChildren[i].SetActive(show);
            }
        }

        private void AutoResolveChildren()
        {
            string[] names = { "BossHealthBar", "HeroHealthBar", "TierLabel", "AbilityBar" };
            var found = new System.Collections.Generic.List<GameObject>(names.Length);
            for (int i = 0; i < names.Length; i++)
            {
                var t = transform.Find(names[i]);
                if (t != null) found.Add(t.gameObject);
            }
            gameplayHudChildren = found.ToArray();
        }
    }
}
