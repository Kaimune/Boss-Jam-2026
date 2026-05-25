using UnityEngine;
using UnityEngine.InputSystem;

namespace BossJam.Game
{
    public class EndOfGame : MonoBehaviour
    {
        [Tooltip("Optional hint (e.g. a 'Press Space' TMP text). Hidden on enable, shown after promptDelaySeconds. Leave null for no prompt.")]
        [SerializeField] private GameObject prompt;

        [Tooltip("Seconds after this object enables before the prompt appears.")]
        [SerializeField, Min(0f)] private float promptDelaySeconds = 5f;

        [Tooltip("Section to activate when Space is pressed (e.g. the Credits child). This object deactivates itself on advance.")]
        [SerializeField] private GameObject nextSection;

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
            if (!promptShown && prompt != null
                && Time.unscaledTime - enabledAt >= promptDelaySeconds)
            {
                prompt.SetActive(true);
                promptShown = true;
            }

            var kb = Keyboard.current;
            if (kb != null && kb.spaceKey.wasPressedThisFrame)
            {
                DisableEverythingElseInScene();
                if (nextSection != null) nextSection.SetActive(true);
                gameObject.SetActive(false);
            }
        }

        // Wipe the rest of the scene — gameplay, HUD, start screen, world —
        // so the credits flow takes over a clean stage. The root containing
        // this EndOfGame (and its sibling Credits screen) is preserved.
        private void DisableEverythingElseInScene()
        {
            var scene = gameObject.scene;
            var keepRoot = transform.root.gameObject;
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i] != keepRoot) roots[i].SetActive(false);
            }
        }
    }
}
