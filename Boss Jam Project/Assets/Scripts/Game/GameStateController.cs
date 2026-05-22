using System;
using BossJam.Cutscene;
using BossJam.Difficulty;
using BossJam.Dialogue;
using BossJam.Player;
using UnityEngine;

namespace BossJam.Game
{
    public enum GameState
    {
        Startup,
        CutsceneIntro,   // letterbox + hero walks in
        Dialogue,        // pre-fight typewriter; timescale 0
        Playing,
        Death,           // hero killed; OutroDirector auto-cutscene running
        GameOver,        // boss killed; OutroDirector auto-cutscene running
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

        [Header("Dialogue")]
        [SerializeField] private DialogueRig dialogueRig;

        [Header("Cutscene")]
        [SerializeField] private IntroDirector introDirector;
        [SerializeField] private OutroDirector outroDirector;

        private GameState postDialogueTarget = GameState.Playing;

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
            if (dialogueRig != null && dialogueRig.Controller != null)
                dialogueRig.Controller.Finished += OnDialogueFinished;
        }

        private void OnDisable()
        {
            if (runtime != null) runtime.HeroKilled -= OnHeroKilled;
            if (dialogueRig != null && dialogueRig.Controller != null)
                dialogueRig.Controller.Finished -= OnDialogueFinished;
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
            EnterCutsceneIntro();
        }

        private void EnterCutsceneIntro()
        {
            Time.timeScale = 1f;
            if (Boss != null) Boss.enabled = false;
            State = GameState.CutsceneIntro;
            StateChanged?.Invoke(State);

            if (introDirector == null)
            {
                EnterPreFightDialogue();
                return;
            }
            introDirector.IntroComplete += OnIntroComplete;
            // HeroSpawner spawns on the StateChanged event we just fired (CS9 wires this).
            // Grab the hero one frame later, then ask the director to begin the walk.
            StartCoroutine(BeginIntroNextFrame());
        }

        private System.Collections.IEnumerator BeginIntroNextFrame()
        {
            yield return null;
            var spawner = FindFirstObjectByType<HeroSpawner>();
            var hero = spawner != null ? spawner.CurrentHero : null;
            introDirector.Begin(hero);
        }

        private void OnIntroComplete()
        {
            if (introDirector != null) introDirector.IntroComplete -= OnIntroComplete;
            EnterPreFightDialogue();
        }

        private void EnterPreFightDialogue()
        {
            int wave = (runtime != null) ? runtime.CurrentWaveIndex : 1;
            string scriptName = $"intro_wave_{wave}";
            postDialogueTarget = GameState.Playing;
            EnterDialogue();
            if (dialogueRig != null) dialogueRig.Play(scriptName);
            else EnterPlaying();
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

        private void EnterDialogue()
        {
            // Pause the boss + freeze time during dialogue so the fight doesn't
            // start under the cutscene. Dialogue uses WaitForSecondsRealtime so
            // a frozen timescale doesn't break the typewriter.
            if (Boss != null) Boss.enabled = false;
            Time.timeScale = 0f;
            State = GameState.Dialogue;
            StateChanged?.Invoke(State);
        }

        private void OnDialogueFinished()
        {
            if (postDialogueTarget == GameState.Playing) EnterPlaying();
            else if (postDialogueTarget == GameState.Death) EnterDeathOutro();
            else if (postDialogueTarget == GameState.GameOver) EnterGameOverOutro();
            else EnterPlaying();
        }

        public void TriggerDeath()
        {
            if (State == GameState.Death) return;
            EnterDeathOutro();
        }

        private void EnterDeathOutro()
        {
            State = GameState.Death;
            Time.timeScale = 0f;
            if (Boss != null) Boss.enabled = false;
            StateChanged?.Invoke(State);

            if (outroDirector == null) { ResumeAfterDeathOutro(); return; }
            outroDirector.OutroComplete += OnDeathOutroComplete;
            int wave = (runtime != null) ? runtime.CurrentWaveIndex : 1;
            outroDirector.PlayHeroDeath(wave);
        }

        private void OnDeathOutroComplete()
        {
            if (outroDirector != null) outroDirector.OutroComplete -= OnDeathOutroComplete;
            ResumeAfterDeathOutro();
        }

        private void ResumeAfterDeathOutro()
        {
            // Commit the queued debuff + respawn boss (existing Resume() logic),
            // then loop back through CutsceneIntro for the next wave.
            if (runtime != null) runtime.ApplyNextDebuff();
            if (Boss != null) Boss.Respawn();
            State = GameState.Startup; // satisfy the Begin() guard
            Begin();
        }

        public void TriggerGameOver()
        {
            if (State == GameState.GameOver) return;
            EnterGameOverOutro();
        }

        private void EnterGameOverOutro()
        {
            State = GameState.GameOver;
            Time.timeScale = 0f;
            if (Boss != null) Boss.enabled = false;
            StateChanged?.Invoke(State);

            if (outroDirector == null) return;
            outroDirector.OutroComplete += OnGameOverOutroComplete;
            outroDirector.PlayBossDeath();
        }

        private void OnGameOverOutroComplete()
        {
            if (outroDirector != null) outroDirector.OutroComplete -= OnGameOverOutroComplete;
            // State stays GameOver. GameOverScreenUI listens for StateChanged
            // and shows its panel; player presses Space to call Resume().
        }

        /// <summary>
        /// Resume from the GameOver screen. Refills the boss (debuff state is
        /// preserved) and loops back through the intro cutscene.
        /// </summary>
        public void Resume()
        {
            if (State != GameState.GameOver) return;
            if (Boss != null) Boss.Respawn();
            State = GameState.Startup; // satisfy the Begin() guard
            Begin();
        }
    }
}
