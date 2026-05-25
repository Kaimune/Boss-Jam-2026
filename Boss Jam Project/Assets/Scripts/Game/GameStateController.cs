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

        [Header("End-of-run")]
        [Tooltip("Root GameObject containing the End of Game and Credits children. Activated after the hero death outro on the final tier (a beaten run).")]
        [SerializeField] private GameObject creditsRoot;

        [Tooltip("Start-screen GameObject to force-disable when the credits handoff activates (e.g. the StartScreen canvas). Optional.")]
        [SerializeField] private GameObject startScreenRoot;

        private bool pendingCreditsHandoff;

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
            if (runtime == null) return;
            // Final-tier kill: player beat the run. Play the death outro
            // (which fires the hero death animation), then hand off to the
            // credits flow instead of advancing tier / reloading.
            if (!runtime.HasNextTier)
            {
                pendingCreditsHandoff = true;
                EnterDeathOutro();
                return;
            }
            TriggerDeath();
        }

        public void Begin()
        {
            if (State != GameState.Startup) return;

            // Respawn fast-path: hero's low-HP warning or boss save-scum sets
            // RunState.skipNextIntro and reloads the scene. On that reload we
            // skip narration / intermediate card / cutscene / pre-fight dialogue
            // entirely, drop straight into Playing, and play the respawn_reload
            // script so the player is back in the fight in one beat.
            if (runtime != null && runtime.State != null && runtime.State.skipNextIntro)
            {
                runtime.State.skipNextIntro = false;
                runtime.State.pendingTierNarration = false; // never replay tier narration on a save-scum reload
                EnterPlaying();
                PlayInGameDialogue("respawn_reload");
                return;
            }

            // Fresh-run auto-advance: the player should never see the empty
            // pre-tier state. On the first wave (no tiers applied yet), commit
            // tier 1 (Impossible) before the difficulty card renders so the
            // card picks up the freshly-applied tier. Mid-run reloads skip
            // this — DifficultyRuntime.Awake already rehydrates from RunState.
            bool freshRunAutoAdvanced = false;
            if (runtime != null
                && runtime.State != null
                && runtime.State.appliedTiers.Count == 0
                && runtime.HasNextTier)
            {
                runtime.AdvanceTier();
                freshRunAutoAdvanced = true;
            }

            // Mid-run: if the just-applied debuff names a narration script,
            // play it before the difficulty card. Fresh runs skip narration
            // and go straight to the difficulty card — narration is reserved
            // for the death→tier-up transitions.
            var entry = runtime != null ? runtime.LastAppliedEntry : null;
            bool wantsNarration =
                !freshRunAutoAdvanced
                && entry != null
                && !string.IsNullOrWhiteSpace(entry.narrationScriptName)
                && runtime.State != null
                && runtime.State.pendingTierNarration;

            // Consume the flag regardless of whether we end up playing — a subsequent
            // reload at the same tier (e.g. boss death) should not replay the narration.
            if (runtime != null && runtime.State != null)
                runtime.State.pendingTierNarration = false;

            if (wantsNarration)
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
            int wave = (runtime != null) ? runtime.AppliedCount : 1;
            string scriptName = $"intro_wave_{wave}";
            postDialogueTarget = GameState.Playing;
            EnterDialogue();
            if (dialogueRig != null) dialogueRig.Play(scriptName);
            else EnterPlaying();
        }

        /// <summary>
        /// Play an ad-hoc dialogue script mid-gameplay. Pauses time + boss while
        /// the dialogue runs; restores Playing state when the dialogue Finishes.
        /// </summary>
        public void PlayInGameDialogue(string scriptName)
        {
            if (string.IsNullOrEmpty(scriptName)) return;
            if (dialogueRig == null || dialogueRig.Controller == null) return;
            if (dialogueRig.Controller.IsPlaying) return;

            // Sit in the existing Dialogue state — the rest of the state machine
            // already pauses Time/Boss correctly. Save where we want to land
            // after the dialogue finishes (back to Playing).
            postDialogueTarget = GameState.Playing;
            State = GameState.Dialogue;
            Time.timeScale = 0f;
            if (Boss != null) Boss.enabled = false;
            StateChanged?.Invoke(State);

            dialogueRig.Controller.Play(scriptName);
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
            // Keep time flowing — the world finishes its in-flight animations.
            // Boss.enabled = false stops new input, and BossController/HeroEnemy
            // TakeDamage state-gates block in-flight damage during the cinematic.
            if (Boss != null) Boss.enabled = false;
            LockMovers();
            StateChanged?.Invoke(State);

            if (outroDirector == null)
            {
                if (pendingCreditsHandoff) ActivateCreditsHandoff();
                else ResumeAfterDeathOutro();
                return;
            }
            outroDirector.OutroComplete += OnDeathOutroComplete;
            int wave = (runtime != null) ? runtime.AppliedCount : 1;
            // Forward the dying hero's DeathFx so the outro sizes its wait to
            // the death clip length (DeathFx.Play() already fired at kill site).
            var hero = FindFirstObjectByType<BossJam.Enemies.HeroEnemy>();
            var heroFx = hero != null ? hero.DeathFx : null;
            outroDirector.PlayHeroDeath(wave, heroFx);
        }

        private void OnDeathOutroComplete()
        {
            if (outroDirector != null) outroDirector.OutroComplete -= OnDeathOutroComplete;
            if (pendingCreditsHandoff) { ActivateCreditsHandoff(); return; }
            ResumeAfterDeathOutro();
        }

        private void ActivateCreditsHandoff()
        {
            pendingCreditsHandoff = false;
            if (startScreenRoot != null) startScreenRoot.SetActive(false);
            if (creditsRoot != null) creditsRoot.SetActive(true);
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

        // Save-scum on lethal damage: play the hero's death clip in place,
        // lock the world like a normal death outro, then reload into the
        // respawn_reload dialogue. Skips the outro line + fade entirely so the
        // beat reads as a system-reload rather than a story moment.
        public void TriggerSaveScumReload(BossJam.Audio.DeathFx heroDeathFx)
        {
            if (State == GameState.Death || State == GameState.GameOver) return;
            StartCoroutine(SaveScumReloadRoutine(heroDeathFx));
        }

        private System.Collections.IEnumerator SaveScumReloadRoutine(BossJam.Audio.DeathFx heroDeathFx)
        {
            State = GameState.Death;
            if (Boss != null) Boss.enabled = false;
            LockMovers();
            StateChanged?.Invoke(State);

            if (heroDeathFx != null) heroDeathFx.Play();
            float wait = (heroDeathFx != null && heroDeathFx.ClipLengthSeconds > 0f)
                ? heroDeathFx.ClipLengthSeconds
                : 0.4f;
            yield return new WaitForSecondsRealtime(wait);

            if (runtime != null && runtime.State != null) runtime.State.skipNextIntro = true;
            ReloadScene();
        }

        private void EnterGameOverOutro()
        {
            State = GameState.GameOver;
            // Keep time flowing — see EnterDeathOutro for the rationale.
            if (Boss != null) Boss.enabled = false;
            LockMovers();
            StateChanged?.Invoke(State);

            if (outroDirector == null) return;
            outroDirector.OutroComplete += OnGameOverOutroComplete;
            int wave = (runtime != null) ? runtime.AppliedCount : 0;
            outroDirector.PlayBossDeath(wave, Boss != null ? Boss.DeathFx : null);
        }

        private void OnGameOverOutroComplete()
        {
            if (outroDirector != null) outroDirector.OutroComplete -= OnGameOverOutroComplete;
            // Boss death just replays the current tier — no GameOver screen, no
            // tier advance. The RunState persists so the player retries the same
            // wave with the same difficulty, but reset the save-scum token so
            // the hero can save-scum once again in the new attempt.
            if (runtime != null && runtime.State != null)
                runtime.State.saveScumAvailable = true;
            ReloadScene();
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

        // Zero out cached InputDirection on every mover in the scene so latent
        // movement (e.g. the player still holding W when the lethal blow lands)
        // doesn't drift another cell after the cinematic starts. Update on the
        // mover only stops emitting motion once InputDirection reads zero;
        // BossController.Update / HeroEnemy.Update have both stopped writing to
        // it by this point.
        private void LockMovers()
        {
            if (Boss != null)
            {
                var bossMover = Boss.GetComponent<GridMover>();
                if (bossMover != null) bossMover.InputDirection = Vector2.zero;
            }
            var hero = FindFirstObjectByType<BossJam.Enemies.HeroEnemy>();
            if (hero != null)
            {
                var heroMover = hero.GetComponent<GridMover>();
                if (heroMover != null) heroMover.InputDirection = Vector2.zero;
            }
        }

        public void ReloadScene()
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
