using BossJam.Difficulty;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BossJam.Game
{
    public class CreditsScreen : MonoBehaviour
    {
        [Tooltip("Optional hint (e.g. a 'Press Space' TMP text). Hidden on enable, shown after promptDelaySeconds.")]
        [SerializeField] private GameObject prompt;

        [Tooltip("Seconds after this object enables before the prompt appears (and Space becomes responsive).")]
        [SerializeField, Min(0f)] private float promptDelaySeconds = 5f;

        private float enabledAt;
        private bool promptShown;

        private void OnEnable()
        {
            enabledAt = Time.unscaledTime;
            promptShown = false;
            if (prompt != null) prompt.SetActive(false);
        }

        private void Update()
        {
            if (Time.unscaledTime - enabledAt < promptDelaySeconds) return;
            if (!promptShown && prompt != null)
            {
                prompt.SetActive(true);
                promptShown = true;
            }
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
