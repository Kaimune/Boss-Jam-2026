using BossJam.Dialogue;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BossJam.Game
{
    /// <summary>
    /// Scene-level glue around DialogueController: toggles the two portrait
    /// cameras during playback, polls the keyboard for fast-forward (Space)
    /// and skip (Esc). Camera transforms are authored by hand in the scene —
    /// this script never reparents or repositions them.
    /// </summary>
    [RequireComponent(typeof(DialogueController))]
    public sealed class DialogueRig : MonoBehaviour
    {
        [SerializeField] private Camera heroPortraitCam;
        [SerializeField] private Camera bossPortraitCam;

        private DialogueController controller;
        public DialogueController Controller => controller;

        private void Awake()
        {
            controller = GetComponent<DialogueController>();
            if (controller != null) controller.Finished += OnDialogueFinished;
            EnableCameras(false);
        }

        private void OnDestroy()
        {
            if (controller != null) controller.Finished -= OnDialogueFinished;
        }

        private void Update()
        {
            if (controller == null || !controller.IsPlaying) return;
            var kb = Keyboard.current;
            if (kb == null) return;
            controller.IsFastForwarding = kb.spaceKey.isPressed;
            if (kb.escapeKey.wasPressedThisFrame) controller.SkipAll();
        }

        public void Play(string scriptName)
        {
            EnableCameras(true);
            controller.Play(scriptName);
        }

        private void OnDialogueFinished() => EnableCameras(false);

        public void EnableCameras(bool on)
        {
            if (heroPortraitCam != null) heroPortraitCam.gameObject.SetActive(on);
            if (bossPortraitCam != null) bossPortraitCam.gameObject.SetActive(on);
        }
    }
}
