using BossJam.Dialogue;
using BossJam.Enemies;
using BossJam.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BossJam.Game
{
    /// <summary>
    /// Scene-level glue around DialogueController: toggles the two portrait
    /// cameras during playback and polls the keyboard for the advance press
    /// (Space). Dialogue is unskippable — there is no abort key. The portrait
    /// cameras are reparented onto each actor's HeadAnchor at runtime so they
    /// rotate with the model — their local position/rotation (the framing
    /// offset) is preserved, so authors set the offset once in the inspector.
    /// </summary>
    [RequireComponent(typeof(DialogueController))]
    public sealed class DialogueRig : MonoBehaviour
    {
        [SerializeField] private Camera heroPortraitCam;
        [SerializeField] private Camera bossPortraitCam;

        [Tooltip("Child transform name on each actor that the portrait cam parents under.")]
        [SerializeField] private string headAnchorName = "HeadAnchor";

        private DialogueController controller;
        public DialogueController Controller => controller;

        private void Awake()
        {
            controller = GetComponent<DialogueController>();
            if (controller != null) controller.Finished += OnDialogueFinished;
            EnableCameras(false);

            // Boss is hand-placed in the scene — anchor its cam once.
            var boss = FindFirstObjectByType<BossController>();
            if (boss != null) ParentCamToHeadAnchor(bossPortraitCam, boss.transform);
        }

        // Hero portrait cam is hand-placed in the scene (parented + offset
        // however you like, relative to the hero). No HeadAnchor reparenting —
        // EnableCameras still toggles it on during dialogue.

        private void OnDestroy()
        {
            if (controller != null) controller.Finished -= OnDialogueFinished;
        }

        private void ParentCamToHeadAnchor(Camera cam, Transform actor)
        {
            if (cam == null || actor == null) return;
            var anchor = actor.Find(headAnchorName);
            if (anchor == null)
            {
                Debug.LogWarning($"{nameof(DialogueRig)}: no '{headAnchorName}' on '{actor.name}'.", actor);
                return;
            }
            cam.transform.SetParent(anchor, worldPositionStays: false);
        }

        private void Update()
        {
            if (controller == null || !controller.IsPlaying) return;
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.spaceKey.wasPressedThisFrame) controller.RequestAdvance();
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
