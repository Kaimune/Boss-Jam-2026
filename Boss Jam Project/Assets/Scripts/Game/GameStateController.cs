using System;
using BossJam.Player;
using UnityEngine;

namespace BossJam.Game
{
    public enum GameState
    {
        Startup,
        Playing,
        Death,
    }

    /// <summary>
    /// Scene-scoped singleton that owns high-level game state transitions and
    /// their side effects (Time.timeScale, BossController.enabled). Subscribers
    /// react to StateChanged for display / audio / etc. — they should not
    /// manipulate Time.timeScale or the boss themselves.
    ///
    /// Lives in GameplayScene alongside DifficultyRuntime. Not DontDestroyOnLoad —
    /// each scene gets its own instance.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameStateController : MonoBehaviour
    {
        public static GameStateController Instance { get; private set; }

        public GameState State { get; private set; } = GameState.Startup;
        public event Action<GameState> StateChanged;

        // Auto-set by BossController.Awake. The setter syncs the boss's enabled
        // flag to the current state so registration order with Awake doesn't
        // matter — whoever wakes last fixes it up.
        private BossController boss;
        public BossController Boss
        {
            get => boss;
            set
            {
                boss = value;
                if (boss != null) boss.enabled = (State == GameState.Playing);
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            // Default state is Startup → time is paused until Begin().
            Time.timeScale = 0f;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                // Restore timeScale so leaving a paused scene doesn't trap the next one.
                Time.timeScale = 1f;
                Instance = null;
            }
        }

        public void Begin()
        {
            if (State != GameState.Startup) return;
            State = GameState.Playing;
            Time.timeScale = 1f;
            if (Boss != null) Boss.enabled = true;
            StateChanged?.Invoke(State);
        }

        public void TriggerDeath()
        {
            if (State == GameState.Death) return;
            State = GameState.Death;
            Time.timeScale = 0f;
            if (Boss != null) Boss.enabled = false;
            StateChanged?.Invoke(State);
        }

        public void Resume()
        {
            if (State != GameState.Death) return;
            // Boss.Respawn() re-enables the controller and refills HP through
            // the current difficulty ledger.
            if (Boss != null) Boss.Respawn();
            Time.timeScale = 1f;
            State = GameState.Playing;
            StateChanged?.Invoke(State);
        }
    }
}
