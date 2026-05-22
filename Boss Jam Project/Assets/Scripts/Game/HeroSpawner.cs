using System;
using BossJam.Enemies;
using UnityEngine;

namespace BossJam.Game
{
    /// <summary>
    /// Owns the hero lifecycle. Listens to GameStateController.StateChanged
    /// and spawns one hero every time the game enters Playing — so the first
    /// hero appears when the player presses Space on the StartScreen, and a
    /// fresh hero appears each time Space is pressed on the tier-advance
    /// screen.
    ///
    /// The hero is a prefab — it self-resolves the boss target and grid via
    /// FindFirstObjectByType in its Awake.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HeroSpawner : MonoBehaviour
    {
        [SerializeField] private HeroEnemy heroPrefab;
        [Tooltip("World position to spawn at. If null, uses this transform's position.")]
        [SerializeField] private Transform spawnPoint;

        /// <summary>
        /// Fires after a fresh hero is instantiated, with the new instance as
        /// payload. Health bars / camera target trackers / etc. subscribe so
        /// they can rebind when the hero changes.
        /// </summary>
        public event Action<HeroEnemy> HeroSpawned;

        public HeroEnemy CurrentHero { get; private set; }

        private GameStateController controller;
        private GameState lastState;

        private void Awake()
        {
            controller = FindFirstObjectByType<GameStateController>();
            lastState = controller != null ? controller.State : GameState.Startup;
        }

        private void OnEnable()
        {
            if (controller != null) controller.StateChanged += OnStateChanged;
        }

        private void OnDisable()
        {
            if (controller != null) controller.StateChanged -= OnStateChanged;
        }

        private void OnStateChanged(GameState next)
        {
            // Spawn on the *entry* to CutsceneIntro. That covers:
            //   Startup       -> CutsceneIntro (first wave)
            //   Death/GameOver -> CutsceneIntro (next wave, via Begin() loop)
            if (next == GameState.CutsceneIntro && lastState != GameState.CutsceneIntro)
            {
                SpawnHero();
            }
            lastState = next;
        }

        private void SpawnHero()
        {
            if (heroPrefab == null)
            {
                Debug.LogWarning($"{nameof(HeroSpawner)}: heroPrefab not assigned.", this);
                return;
            }
            // Clean up any existing hero — covers the "previous wave hero still alive" case.
            var existing = FindObjectsByType<HeroEnemy>(FindObjectsSortMode.None);
            for (int i = 0; i < existing.Length; i++) Destroy(existing[i].gameObject);

            // Prefer the cutscene intro spawn point when an IntroDirector is in the scene.
            Transform overridePoint = null;
            var intro = FindFirstObjectByType<BossJam.Cutscene.IntroDirector>();
            if (intro != null && intro.HeroIntroSpawn != null)
                overridePoint = intro.HeroIntroSpawn;

            var sp = overridePoint != null ? overridePoint : spawnPoint;
            var pos = sp != null ? sp.position : transform.position;
            var rot = sp != null ? sp.rotation : Quaternion.identity;

            CurrentHero = Instantiate(heroPrefab, pos, rot);
            HeroSpawned?.Invoke(CurrentHero);
        }
    }
}
