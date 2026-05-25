using BossJam.Difficulty;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BossJam.Game
{
    public class CreditsScreen : MonoBehaviour
    {
        [Tooltip("Seconds after this object enables before Space becomes responsive.")]
        [SerializeField, Min(0f)] private float inputDelaySeconds = 5f;

        private float enabledAt;

        private void OnEnable() => enabledAt = Time.unscaledTime;

        private void Update()
        {
            if (Time.unscaledTime - enabledAt < inputDelaySeconds) return;
            var kb = Keyboard.current;
            if (kb == null || !kb.spaceKey.wasPressedThisFrame) return;

            // EndOfGame disables most scene roots before activating us, so
            // include inactive when locating the runtime — its GameObject may
            // be sitting on a deactivated root.
            var rt = FindFirstObjectByType<DifficultyRuntime>(FindObjectsInactive.Include);
            if (rt != null && rt.State != null) rt.State.ResetForNewRun();

            var gsc = GameStateController.Instance;
            if (gsc != null) gsc.ReloadScene();
        }
    }
}
