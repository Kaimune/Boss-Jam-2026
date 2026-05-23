using System;
using System.Collections;
using BossJam.CameraSys;
using BossJam.Enemies;
using BossJam.GridSystem;
using UnityEngine;

namespace BossJam.Cutscene
{
    /// <summary>
    /// Walks the AI hero from an off-grid spawn point into a fight-start cell on
    /// the grid. The walk axis is whichever of X/Z has the larger spawn→fight
    /// delta, so the intro can come "from below the camera" (Z) or from the side
    /// (X) without code changes — just by moving the HeroIntroSpawn transform.
    /// On arrival, registers the hero with the grid and raises IntroComplete.
    /// Esc skips to the end via SnapToEnd().
    ///
    /// Optionally widens the gameplay camera (CameraFollow + ortho size) during
    /// the walk for a cinematic pull-back, then eases back on completion.
    /// </summary>
    public sealed class IntroDirector : MonoBehaviour
    {
        [Tooltip("Optional teleport point applied at intro start. Leave null when the hero is hand-placed " +
                 "in the scene and you want the walk to start from that placed position.")]
        [SerializeField] private Transform heroIntroSpawn;
        [SerializeField] private Transform heroFightStart;
        [SerializeField] private BossGrid grid;

        [Header("Cutscene pacing")]
        [SerializeField, Tooltip("Per-cell step duration during the walk = grid.TickDuration * this. " +
                                 ">1 makes the cutscene walk slower than in-fight movement.")]
        private float cutsceneStepMultiplier = 2.5f;

        [SerializeField, Tooltip("Animator playback speed multiplier applied to the hero during the " +
                                 "cutscene walk. Restored to 1 when the intro completes.")]
        private float cutsceneAnimatorSpeed = 0.5f;

        [SerializeField, Tooltip("Rotate the hero to face the walk direction at intro start. " +
                                 "Disable if the prefab's idle facing already reads right.")]
        private bool faceWalkDirection = true;

        [Header("Camera framing (optional)")]
        [SerializeField, Tooltip("If set, the camera pulls back to a wider framing during the walk.")]
        private CameraFollow cameraFollow;

        [SerializeField, Tooltip("Camera offset (relative to follow target) held during Startup + the " +
                                 "intro cutscene. Snapped on at scene load so the player never sees the " +
                                 "narrow gameplay framing during the start screen / walk-in.")]
        private Vector3 cutsceneCameraOffset = new Vector3(0f, 9f, -7f);

        [SerializeField, Min(0.1f), Tooltip("Orthographic size held during Startup + the intro cutscene.")]
        private float cutsceneOrthoSize = 12f;

        [SerializeField, Tooltip("Camera offset eased toward when the intro completes — the gameplay " +
                                 "framing the player fights in.")]
        private Vector3 gameplayCameraOffset = new Vector3(0f, 5f, -3.5f);

        [SerializeField, Min(0.1f), Tooltip("Orthographic size eased toward on intro complete (the gameplay framing).")]
        private float gameplayOrthoSize = 6f;

        [SerializeField, Min(0f), Tooltip("Seconds to ease ortho size + offset back to gameplay on Complete.")]
        private float cameraEaseOutSeconds = 0.7f;

        public event Action IntroComplete;
        public Transform HeroIntroSpawn => heroIntroSpawn;
        public Transform HeroFightStart => heroFightStart;

        private Coroutine walkRoutine;
        private Coroutine cameraRoutine;
        private HeroEnemy hero;
        private GridFootprint footprint;
        private Animator heroAnimator;

        private Camera framedCamera;

        private void Awake()
        {
            if (grid == null) grid = FindFirstObjectByType<BossGrid>();
            if (cameraFollow == null) cameraFollow = FindFirstObjectByType<CameraFollow>();
            // Hold the wide cinematic framing from scene load onward so the
            // start screen sits over the wide shot and the hero walks into it.
            // The narrow gameplay framing is eased in on intro complete.
            ApplyCutsceneFraming();
        }

        public void Begin(HeroEnemy heroInstance)
        {
            if (heroInstance == null)
            {
                Debug.LogError($"{nameof(IntroDirector)}: Begin called with null hero.");
                Complete();
                return;
            }
            hero = heroInstance;
            footprint = hero.GetComponent<GridFootprint>();
            heroAnimator = hero.GetComponentInChildren<Animator>();

            // Park the hero off-grid + disable grid registration during walk.
            if (footprint != null) footprint.enabled = false;
            ToggleCombatComponents(false);
            if (heroIntroSpawn != null) hero.transform.position = heroIntroSpawn.position;

            if (faceWalkDirection) ApplyWalkFacing();

            // Slow the walk animation for cinematic pacing. Restored in Complete().
            if (heroAnimator != null) heroAnimator.speed = cutsceneAnimatorSpeed;

            walkRoutine = StartCoroutine(WalkIn());
        }

        public void SnapToEnd()
        {
            if (walkRoutine != null) { StopCoroutine(walkRoutine); walkRoutine = null; }
            Complete();
        }

        private IEnumerator WalkIn()
        {
            if (hero == null || grid == null || heroFightStart == null) { Complete(); yield break; }

            float cellSize = grid.CellSize;
            float tick = grid.TickDuration;
            if (tick <= 0.001f) tick = 0.2f;
            tick *= Mathf.Max(0.01f, cutsceneStepMultiplier);

            // Pick the dominant axis once — the walk is a straight line along
            // whichever of X/Z has the larger spawn→fight delta.
            Vector3 totalDelta = heroFightStart.position - hero.transform.position;
            bool walkAlongZ = Mathf.Abs(totalDelta.z) > Mathf.Abs(totalDelta.x);

            while (true)
            {
                Vector3 here = hero.transform.position;
                Vector3 target = heroFightStart.position;
                float delta = walkAlongZ ? (target.z - here.z) : (target.x - here.x);
                if (Mathf.Abs(delta) < cellSize * 0.5f) break;

                Vector3 step = walkAlongZ
                    ? new Vector3(0f, 0f, Mathf.Sign(delta) * cellSize)
                    : new Vector3(Mathf.Sign(delta) * cellSize, 0f, 0f);
                Vector3 stepEnd = here + step;

                float t = 0f;
                while (t < tick)
                {
                    t += Time.deltaTime;
                    hero.transform.position = Vector3.Lerp(here, stepEnd, Mathf.Clamp01(t / tick));
                    yield return null;
                }
                hero.transform.position = stepEnd;
            }
            Complete();
        }

        private void Complete()
        {
            if (hero != null && grid != null && heroFightStart != null)
            {
                hero.transform.position = heroFightStart.position;
                if (footprint != null)
                {
                    var cell = grid.WorldToCell(heroFightStart.position);
                    footprint.Configure(new Vector2(cell.x, cell.y), footprint.Footprint, grid);
                    footprint.enabled = true;
                }
                // ToggleCombatComponents re-enables GridMover. Its cached anchor
                // is still the pre-walk corner cell, so the first Update would
                // snap the hero back. Sync from the just-registered footprint
                // before re-enabling.
                var gm = hero.GetComponent<BossJam.Player.GridMover>();
                if (gm != null) gm.SyncFromFootprint();
                ToggleCombatComponents(true);
            }
            if (heroAnimator != null) heroAnimator.speed = 1f;
            RestoreGameplayFraming();
            IntroComplete?.Invoke();
        }

        private void ApplyWalkFacing()
        {
            if (hero == null || heroFightStart == null || heroIntroSpawn == null) return;
            Vector3 dir = heroFightStart.position - heroIntroSpawn.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;
            hero.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
        }

        private void ApplyCutsceneFraming()
        {
            if (cameraFollow == null) return;
            framedCamera = cameraFollow.GetComponent<Camera>();
            cameraFollow.Offset = cutsceneCameraOffset;
            if (framedCamera != null && framedCamera.orthographic)
                framedCamera.orthographicSize = cutsceneOrthoSize;
        }

        private void RestoreGameplayFraming()
        {
            if (cameraFollow != null) cameraFollow.Offset = gameplayCameraOffset;
            if (framedCamera != null && framedCamera.orthographic)
            {
                if (cameraRoutine != null) StopCoroutine(cameraRoutine);
                cameraRoutine = StartCoroutine(EaseOrthoSize(framedCamera.orthographicSize, gameplayOrthoSize, cameraEaseOutSeconds));
            }
        }

        private IEnumerator EaseOrthoSize(float from, float to, float seconds)
        {
            if (framedCamera == null) yield break;
            if (seconds <= 0.0001f) { framedCamera.orthographicSize = to; yield break; }
            float t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / seconds));
                framedCamera.orthographicSize = Mathf.Lerp(from, to, u);
                yield return null;
            }
            framedCamera.orthographicSize = to;
            cameraRoutine = null;
        }

        private void ToggleCombatComponents(bool enabledFlag)
        {
            if (hero == null) return;
            // HeroKiteSteering is a static utility (not a component) — nothing to toggle there.
            // HeroEnemy.Update already early-returns when state != Playing, so the static
            // helper isn't called during the cutscene anyway.
            ToggleByTypeName(hero.gameObject, "BossJam.Enemies.FireballSpawner", enabledFlag);

            // GridMover snaps transform.position to its cached anchor every frame.
            // If we leave it enabled during the walk, it overrides the lerped position
            // in WalkIn and the hero appears to "snap back" to its scene-authored cell.
            var gm = hero.GetComponent<BossJam.Player.GridMover>();
            if (gm != null) gm.enabled = enabledFlag;
        }

        private static void ToggleByTypeName(GameObject go, string fullName, bool enabledFlag)
        {
            var t = System.Type.GetType(fullName + ", Assembly-CSharp");
            if (t == null) return;
            // Skip non-component types — guards against static utility classes living
            // in the same assembly accidentally matching by name.
            if (!typeof(Component).IsAssignableFrom(t)) return;
            var c = go.GetComponent(t) as Behaviour;
            if (c != null) c.enabled = enabledFlag;
        }
    }
}
