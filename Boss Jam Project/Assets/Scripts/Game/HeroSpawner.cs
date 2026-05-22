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
            // Spawn when transitioning *into* Playing — that covers both
            // Startup→Playing (game start) and Death→Playing (tier advance).
            if (next == GameState.Playing && lastState != GameState.Playing)
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
            // Clean up any existing hero — covers the GameOver → Playing case
            // where the hero who killed the boss is still alive on screen.
            var existing = FindObjectsByType<HeroEnemy>(FindObjectsSortMode.None);
            for (int i = 0; i < existing.Length; i++) Destroy(existing[i].gameObject);

            var pos = spawnPoint != null ? spawnPoint.position : transform.position;
            var rot = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;
            Instantiate(heroPrefab, pos, rot);
        }
    }
}
