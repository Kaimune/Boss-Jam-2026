using System;
using BossJam.Difficulty;
using BossJam.Player;
using UnityEngine;

namespace BossJam.Game
{
    public enum GameState
    {
        Startup,
        Playing,
        Death,       // hero killed — tier-advance screen pending
        GameOver,    // boss killed — restart-from-current-tier screen pending
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

        private DifficultyRuntime runtime;

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

            runtime = FindFirstObjectByType<DifficultyRuntime>();
        }

        private void OnEnable()
        {
            if (runtime != null) runtime.HeroKilled += OnHeroKilled;
        }

        private void OnDisable()
        {
            if (runtime != null) runtime.HeroKilled -= OnHeroKilled;
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

        private void OnHeroKilled()
        {
            // Only pause for a tier-advance screen if there's a debuff queued
            // to apply. Once the curve is exhausted, hero deaths are silent.
            if (runtime == null || !runtime.HasNextDebuff) return;
            TriggerDeath();
        }

        public void Begin()
        {
            if (State != GameState.Startup) return;
            EnterPlaying();
        }

        // Single source of truth for "transitioning into Playing". Anything
        // that resumes gameplay routes through here so the boss is re-enabled
        // and time resumes consistently.
        private void EnterPlaying()
        {
            if (Boss != null) Boss.enabled = true;
            Time.timeScale = 1f;
            State = GameState.Playing;
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

        public void TriggerGameOver()
        {
            if (State == GameState.GameOver) return;
            State = GameState.GameOver;
            Time.timeScale = 0f;
            if (Boss != null) Boss.enabled = false;
            StateChanged?.Invoke(State);
        }

        /// <summary>
        /// Single resume path used by both the death (hero-killed) screen and
        /// the game-over (boss-killed) screen. Each branch does the state-
        /// specific commit, then we hand off to Playing — the HeroSpawner
        /// reacts to the StateChanged event and spawns a fresh hero.
        /// </summary>
        public void Resume()
        {
            switch (State)
            {
                case GameState.Death:
                    // Tier-advance: commit the queued debuff, then refill the
                    // boss so the new tier starts with full HP.
                    if (runtime != null) runtime.ApplyNextDebuff();
                    if (Boss != null) Boss.Respawn();
                    break;
                case GameState.GameOver:
                    // Restart from current tier: refill the boss, debuff state
                    // is preserved (so debuffs already applied still apply).
                    if (Boss != null) Boss.Respawn();
                    break;
                default:
                    return;
            }
            EnterPlaying();
        }
    }
}
