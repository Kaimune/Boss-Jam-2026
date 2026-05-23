using BossJam.Dialogue;
using BossJam.Enemies;
using BossJam.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BossJam.Game
{
    /// <summary>
    /// Scene-level glue around DialogueController: toggles the two portrait
    /// cameras during playback, polls the keyboard for fast-forward (Space)
    /// and skip (Esc). The portrait cameras are reparented onto each actor's
    /// HeadAnchor at runtime so they rotate with the model — their local
    /// position/rotation (the framing offset) is preserved, so authors set
    /// the offset once in the inspector.
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

        private HeroSpawner heroSpawner;

        private void Awake()
        {
            controller = GetComponent<DialogueController>();
            if (controller != null) controller.Finished += OnDialogueFinished;
            EnableCameras(false);

            // Boss is hand-placed in the scene — anchor its cam once.
            var boss = FindFirstObjectByType<BossController>();
            if (boss != null) ParentCamToHeadAnchor(bossPortraitCam, boss.transform);

            // Hero is spawned per wave — re-parent each time a new one appears.
            heroSpawner = FindFirstObjectByType<HeroSpawner>();
            if (heroSpawner != null) heroSpawner.HeroSpawned += OnHeroSpawned;
        }

        private void OnDestroy()
        {
            if (controller != null) controller.Finished -= OnDialogueFinished;
            if (heroSpawner != null) heroSpawner.HeroSpawned -= OnHeroSpawned;
        }

        private void OnHeroSpawned(HeroEnemy hero)
        {
            if (hero != null) ParentCamToHeadAnchor(heroPortraitCam, hero.transform);
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
