using BossJam.Dialogue;
using BossJam.Enemies;
using BossJam.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BossJam.Game
{
    /// <summary>
    /// Scene-level glue: reparents the two portrait cameras to the live hero
    /// and boss HeadAnchors, forwards advance/skip input to the
    /// DialogueController, and toggles the cameras on/off around playback.
    /// Sits on the same GameObject as DialogueController in GameplayScene.
    /// </summary>
    [RequireComponent(typeof(DialogueController))]
    public sealed class DialogueRig : MonoBehaviour
    {
        [SerializeField] private Camera heroPortraitCam;
        [SerializeField] private Camera bossPortraitCam;
        [SerializeField] private string headAnchorName = "HeadAnchor";

        [Header("Input")]
        [SerializeField] private InputActionReference advanceAction;
        [SerializeField] private InputActionReference skipAction;

        private DialogueController controller;
        private HeroSpawner spawner;

        private void Awake()
        {
            controller = GetComponent<DialogueController>();
            spawner = FindFirstObjectByType<HeroSpawner>();
            if (controller != null) controller.Finished += OnDialogueFinished;
        }

        private void OnEnable()
        {
            if (spawner != null) spawner.HeroSpawned += OnHeroSpawned;
            if (advanceAction != null) { advanceAction.action.Enable(); advanceAction.action.performed += OnAdvance; }
            if (skipAction != null)    { skipAction.action.Enable();    skipAction.action.performed    += OnSkip; }
            AttachBossCamera();
        }

        private void OnDisable()
        {
            if (spawner != null) spawner.HeroSpawned -= OnHeroSpawned;
            if (advanceAction != null) advanceAction.action.performed -= OnAdvance;
            if (skipAction != null)    skipAction.action.performed    -= OnSkip;
        }

        private void OnDestroy()
        {
            if (controller != null) controller.Finished -= OnDialogueFinished;
        }

        public void EnableCameras(bool on)
        {
            if (heroPortraitCam != null) heroPortraitCam.gameObject.SetActive(on);
            if (bossPortraitCam != null) bossPortraitCam.gameObject.SetActive(on);
        }

        public DialogueController Controller => controller;

        public void Play(string scriptName)
        {
            EnableCameras(true);
            controller.Play(scriptName);
        }

        private void OnDialogueFinished()
        {
            EnableCameras(false);
        }

        private void OnHeroSpawned(HeroEnemy hero)
        {
            if (heroPortraitCam == null || hero == null) return;
            ReparentCameraTo(heroPortraitCam, hero.transform);
        }

        private void AttachBossCamera()
        {
            if (bossPortraitCam == null) return;
            var boss = FindFirstObjectByType<BossController>();
            if (boss == null) return;
            ReparentCameraTo(bossPortraitCam, boss.transform);
        }

        private void ReparentCameraTo(Camera cam, Transform root)
        {
            var anchor = FindChildRecursive(root, headAnchorName);
            if (anchor == null)
            {
                Debug.LogWarning($"{nameof(DialogueRig)}: '{headAnchorName}' not found under {root.name}; falling back to root + (0,1.6,0).");
                cam.transform.SetParent(root, false);
                cam.transform.localPosition = new Vector3(0f, 1.6f, -0.6f);
                cam.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                return;
            }
            cam.transform.SetParent(anchor, false);
            cam.transform.localPosition = new Vector3(0f, 0f, -0.6f);
            cam.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        }

        private static Transform FindChildRecursive(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var match = FindChildRecursive(root.GetChild(i), name);
                if (match != null) return match;
            }
            return null;
        }

        private void Update()
        {
            if (controller == null) return;
            controller.IsFastForwarding = advanceAction != null && advanceAction.action.IsPressed();
        }

        private void OnAdvance(InputAction.CallbackContext ctx) { /* fast-forward handled in Update via IsPressed */ }
        private void OnSkip(InputAction.CallbackContext ctx)    => controller?.SkipAll();
    }
}
