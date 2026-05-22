using BossJam.Game;
using UnityEngine;

namespace BossJam.UI
{
    /// <summary>
    /// Hides the HUD whenever the game is not in Playing — covers Startup,
    /// Dialogue, Death, and GameOver. Attached to the HUDCanvas root.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public sealed class HudVisibility : MonoBehaviour
    {
        private Canvas canvas;
        private GameStateController controller;

        private void Awake()
        {
            canvas = GetComponent<Canvas>();
            controller = FindFirstObjectByType<GameStateController>();
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
            if (canvas != null) canvas.enabled = state == GameState.Playing;
        }
    }
}
