using System;
using BossJam.Cutscene;
using BossJam.Difficulty;
using BossJam.Dialogue;
using BossJam.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace BossJam.Game
{
    public enum GameState
    {
        Startup,
        Narration,       // black-screen flavour text shown before the difficulty card on the next wave
        Intermediate,    // difficulty card between title screen and cutscene; timescale 0
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

        [Header("Narration")]
        [SerializeField] private NarrationController narrationController;

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

        private DialogueController subscribedController;

        private void OnEnable()
        {
            if (runtime != null) runtime.HeroKilled += OnHeroKilled;
        }

        private void Start()
        {
            // Wire dialogue Finished here, not in OnEnable: by Start time every
            // other script's Awake has run, so DialogueRig.Controller is set.
            // OnEnable runs before sibling Awakes complete in some edit-time
            // configurations, which silently dropped the subscription.
            if (dialogueRig != null && dialogueRig.Controller != null)
            {
                subscribedController = dialogueRig.Controller;
                subscribedController.Finished += OnDialogueFinished;
            }
            else
            {
                Debug.LogWarning($"{nameof(GameStateController)}: dialogueRig/Controller missing — dialogue→Playing transition will not run.");
            }

            // Every wave goes through the start screen — even mid-run. The
            // start screen handles the tier transition + Press SPACE prompt
            // and then calls Begin() itself.
        }

        private void OnDisable()
        {
            if (runtime != null) runtime.HeroKilled -= OnHeroKilled;
            if (subscribedController != null)
            {
                subscribedController.Finished -= OnDialogueFinished;
                subscribedController = null;
            }
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

        private void Update()
        {
            // Belt-and-suspenders: if the Finished event ever fails to fire
            // (or subscription got dropped), poll the controller so dialogue
            // never strands the state machine in Dialogue.
            if (State == GameState.Dialogue
                && dialogueRig != null
                && dialogueRig.Controller != null
                && !dialogueRig.Controller.IsPlaying)
            {
                OnDialogueFinished();
            }

            // Narration is owned here — forward Space to the controller (the
            // Dialogue asmdef doesn't reference InputSystem so the controller
            // can't poll input itself).
            if (State == GameState.Narration && narrationController != null)
            {
                var kb = Keyboard.current;
                if (kb != null && kb.spaceKey.wasPressedThisFrame)
                    narrationController.RequestAdvance();
            }
        }

        private void OnHeroKilled()
        {
            // Only pause for a tier-advance screen if there's a debuff queued
            // to apply. Once the curve is exhausted, hero deaths are silent.
            if (runtime == null || !runtime.HasNextTier) return;
            TriggerDeath();
        }

        public void Begin()
        {
            if (State != GameState.Startup) return;
            // Mid-run: if the just-applied debuff names a narration script,
            // play it before the difficulty card. Fresh runs (no LastAppliedEntry)
            // and entries with no script name skip straight to Intermediate.
            var entry = runtime != null ? runtime.LastAppliedEntry : null;
            if (entry != null && !string.IsNullOrWhiteSpace(entry.narrationScriptName))
                EnterNarration(entry.narrationScriptName);
            else
                EnterIntermediate();
        }

        // Called by IntermediateScreenUI after the player presses SPACE on the
        // difficulty card. Drives the next transition into the cutscene.
        public void AdvanceFromIntermediate()
        {
            if (State != GameState.Intermediate) return;
            EnterCutsceneIntro();
        }

        private void EnterNarration(string scriptName)
        {
            Time.timeScale = 0f;
            if (Boss != null) Boss.enabled = false;
            State = GameState.Narration;
            StateChanged?.Invoke(State);

            if (narrationController == null)
            {
                Debug.LogWarning($"{nameof(GameStateController)}: NarrationController not assigned; skipping narration '{scriptName}'.");
                EnterIntermediate();
                return;
            }
            narrationController.Finished += OnNarrationFinished;
            narrationController.Play(scriptName);
        }

        private void OnNarrationFinished()
        {
            if (narrationController != null) narrationController.Finished -= OnNarrationFinished;
            if (State != GameState.Narration) return;
            EnterIntermediate();
        }

        private void EnterIntermediate()
        {
            // Time stays paused — Startup → Intermediate is purely a UI hop
            // and the boss must remain disabled.
            Time.timeScale = 0f;
            if (Boss != null) Boss.enabled = false;
            State = GameState.Intermediate;
            StateChanged?.Invoke(State);
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
            // Hero is hand-placed in the scene; grab it one frame after the
            // state change so any wave-1 init (HeroEnemy.Awake) is finished.
            StartCoroutine(BeginIntroNextFrame());
        }

        private System.Collections.IEnumerator BeginIntroNextFrame()
        {
            yield return null;
            var hero = FindFirstObjectByType<BossJam.Enemies.HeroEnemy>(FindObjectsInactive.Include);
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
            // Commit the queued debuff to the persistent RunState, then reload
            // the scene. Every actor and subscriber rebuilds from scratch on
            // the next frame; DifficultyRuntime rehydrates the modifier ledger
            // from RunState in its Awake. GameStateController.Start sees
            // IsMidRun and auto-calls Begin() to roll into the next cutscene.
            if (runtime != null) runtime.AdvanceTier();
            ReloadScene();
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
        /// Resume from the GameOver screen. Reloads the scene; RunState
        /// (debuffs + wave index) persists so the player retries the same
        /// wave with the same difficulty.
        /// </summary>
        public void Resume()
        {
            if (State != GameState.GameOver) return;
            ReloadScene();
        }

        private void ReloadScene()
        {
            // Restore timescale before reloading — OnDestroy will run during
            // the scene unload, but doing it explicitly first means any code
            // that observes timescale during the unload sees a sane value.
            Time.timeScale = 1f;
            var scene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(scene.buildIndex);
        }
    }
}
