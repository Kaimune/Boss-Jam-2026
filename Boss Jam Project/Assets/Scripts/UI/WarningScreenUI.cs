using System.Collections;
using BossJam.Enemies;
using BossJam.Game;
using BossJam.Player;
using TMPro;
using UnityEngine;

namespace BossJam.UI
{
    /// <summary>
    /// Low-HP warning overlay. While the hero's save-scum warning is armed
    /// (HP at or below the warning threshold, counting down to a reload),
    /// shows <see cref="warningPanel"/> and prints the remaining seconds into
    /// <see cref="secondsTimerText"/>. Hides itself outside GameState.Playing
    /// so it doesn't bleed through dialogue / death / start screen.
    ///
    /// On the frame the warning arms (off → on), the panel does a single
    /// scale-punch flash to grab the player's attention — appears at full
    /// scale × flashScale and eases back to scale 1 over flashSeconds.
    ///
    /// Attach to the HUDCanvas (or any always-on UI root) and wire:
    ///   - warningPanel       → the "Warning Screen" GameObject (the thing
    ///                          that turns on while the timer is ticking).
    ///   - secondsTimerText   → the "Seconds Timer" TMP_Text child.
    /// The hero is auto-resolved from the scene; each wave reload rebinds.
    /// </summary>
    public sealed class WarningScreenUI : MonoBehaviour
    {
        [SerializeField] private GameObject warningPanel;
        [SerializeField] private TMP_Text secondsTimerText;
        [Tooltip("Format string for the seconds-remaining label. {0} is the float seconds remaining.")]
        [SerializeField] private string secondsFormat = "{0:0.00}";

        [Header("On-arm flash")]
        [Tooltip("Duration (real seconds) of the scale-punch flash when the warning first arms. 0 = no flash.")]
        [SerializeField, Min(0f)] private float flashSeconds = 0.22f;
        [Tooltip("Scale the panel punches up to at the start of the flash; eases back to 1 over flashSeconds. 1 = no punch.")]
        [SerializeField, Min(1f)] private float flashScale = 1.35f;
        [Tooltip("Optional beep SFX. Plays the moment the warning arms and then repeats every BlinkSfxIntervalSeconds while armed.")]
        [SerializeField] private AudioClip blinkSfx;
        [Tooltip("Seconds between repeated beeps while the warning stays armed.")]
        [SerializeField, Min(0.01f)] private float blinkSfxIntervalSeconds = 0.5f;
        [Tooltip("Volume of the beep (0 = silent, 1 = full).")]
        [SerializeField, Range(0f, 1f)] private float blinkSfxVolume = 1f;
        [Tooltip("Optional dedicated AudioSource for the beep. If unassigned, one is auto-created on this GameObject. Owned-source design lets us Stop() the beep cleanly the instant the warning ends — needed because PlayOneShot through a shared source can't be cancelled.")]
        [SerializeField] private AudioSource sfxSource;

        private HeroEnemy hero;
        private GameStateController gameState;

        private CanvasGroup warningCanvasGroup;
        private bool prevArmed;
        private bool flashing;
        private Coroutine flashRoutine;
        private float nextBlinkSfxAt;

        private bool subscribedToStateChanged;

        // Latched at the moment either combatant dies. Once true, the warning
        // panel stays force-off + the beep stays muted for the rest of this
        // scene instance, regardless of game state or armed flag. The flag
        // resets naturally on the next scene load (fresh WarningScreenUI).
        private bool permanentlyDisabled;
        private BossController boss;
        private bool subscribedHeroKilled;
        private bool subscribedBossDied;

        private void Start()
        {
            hero = FindFirstObjectByType<HeroEnemy>(FindObjectsInactive.Include);
            if (warningPanel != null)
                warningCanvasGroup = warningPanel.GetComponent<CanvasGroup>();
            EnsureSfxSource();
            SetPanelActive(false);
            TryBindGameState();
            TryBindDeathEvents();
        }

        private void EnsureSfxSource()
        {
            if (sfxSource != null) return;
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.loop = false;
            sfxSource.spatialBlend = 0f; // 2D
        }

        private void OnDestroy()
        {
            if (gameState != null && subscribedToStateChanged)
                gameState.StateChanged -= OnStateChanged;
            if (subscribedHeroKilled) HeroEnemy.HeroKilledStatic -= OnAnyoneDied;
            if (boss != null && subscribedBossDied) boss.BossDied -= OnAnyoneDied;
        }

        private void TryBindGameState()
        {
            if (subscribedToStateChanged) return;
            if (gameState == null) gameState = GameStateController.Instance;
            if (gameState == null) return;
            gameState.StateChanged += OnStateChanged;
            subscribedToStateChanged = true;
        }

        private void TryBindDeathEvents()
        {
            if (!subscribedHeroKilled)
            {
                HeroEnemy.HeroKilledStatic += OnAnyoneDied;
                subscribedHeroKilled = true;
            }
            if (boss == null) boss = FindFirstObjectByType<BossController>(FindObjectsInactive.Include);
            if (boss != null && !subscribedBossDied)
            {
                boss.BossDied += OnAnyoneDied;
                subscribedBossDied = true;
            }
        }

        // Hard-off latch: fires on hero or boss death. Once tripped, Update
        // re-asserts panel-off / sfx-stop every frame for the rest of this
        // scene instance. No path can re-arm the warning until scene reload.
        private void OnAnyoneDied()
        {
            permanentlyDisabled = true;
            if (flashRoutine != null) { StopCoroutine(flashRoutine); flashRoutine = null; }
            flashing = false;
            prevArmed = false;
            SetPanelActive(false);
            if (sfxSource != null && sfxSource.isPlaying) sfxSource.Stop();
            nextBlinkSfxAt = float.PositiveInfinity;
        }

        // Kill-switch: the moment the game leaves Playing, snap everything off.
        // We deliberately do NOT disable the component — Update keeps running
        // and re-asserts "panel off" every frame, so any external system that
        // tries to re-activate the warning panel during the outro gets undone
        // on the very next tick.
        private void OnStateChanged(GameState state)
        {
            if (state == GameState.Playing)
            {
                if (nextBlinkSfxAt == float.PositiveInfinity) nextBlinkSfxAt = 0f;
                return;
            }
            if (flashRoutine != null) { StopCoroutine(flashRoutine); flashRoutine = null; }
            flashing = false;
            prevArmed = false;
            SetPanelActive(false);
            if (sfxSource != null) sfxSource.Stop();
            nextBlinkSfxAt = float.PositiveInfinity;
        }

        private void Update()
        {
            // Lazy rebind — Instance / hero may not exist on the very first
            // frame after a scene reload; re-resolve until they show up.
            if (gameState == null) TryBindGameState();
            if (hero == null) hero = FindFirstObjectByType<HeroEnemy>(FindObjectsInactive.Include);
            if (boss == null || !subscribedBossDied) TryBindDeathEvents();

            // Hard-off after any death this scene — force everything off and
            // exit before any armed-driven path can re-flip the panel.
            if (permanentlyDisabled)
            {
                if (flashRoutine != null) { StopCoroutine(flashRoutine); flashRoutine = null; }
                flashing = false;
                prevArmed = false;
                SetPanelActive(false);
                if (sfxSource != null && sfxSource.isPlaying) sfxSource.Stop();
                nextBlinkSfxAt = float.PositiveInfinity;
                return;
            }

            bool playing = gameState != null && gameState.State == GameState.Playing;

            // Hard floor while not in Playing: re-assert "panel off" every
            // frame and kill any in-flight beep, so anything else flipping
            // the panel on (HudVisibility, a child controller, a stray
            // SetActive) gets undone on the next tick. No flash, no beep,
            // no timer text updates leak into the outro / credits / title.
            if (!playing)
            {
                if (flashRoutine != null) { StopCoroutine(flashRoutine); flashRoutine = null; }
                flashing = false;
                prevArmed = false;
                SetPanelActive(false);
                if (sfxSource != null && sfxSource.isPlaying) sfxSource.Stop();
                nextBlinkSfxAt = float.PositiveInfinity;
                return;
            }

            // Coming back into Playing — clear the stale infinite gate.
            if (nextBlinkSfxAt == float.PositiveInfinity) nextBlinkSfxAt = 0f;

            bool armed = hero != null && hero.IsRespawnWarningActive;

            // Rising edge: kick the flash + fire the first beep immediately.
            if (armed && !prevArmed)
            {
                if (blinkSfx != null)
                {
                    PlayBeep();
                    nextBlinkSfxAt = Time.unscaledTime + blinkSfxIntervalSeconds;
                }
                if (flashSeconds > 0f && flashScale > 1f)
                {
                    if (flashRoutine != null) StopCoroutine(flashRoutine);
                    flashRoutine = StartCoroutine(FlashRoutine());
                }
            }
            else if (armed && blinkSfx != null && Time.unscaledTime >= nextBlinkSfxAt)
            {
                // Repeating beep while armed.
                PlayBeep();
                nextBlinkSfxAt = Time.unscaledTime + blinkSfxIntervalSeconds;
            }
            prevArmed = armed;

            // The flash coroutine owns the panel scale while it runs;
            // outside that, drive the panel directly off the armed flag.
            if (!flashing) SetPanelActive(armed);

            if (armed && secondsTimerText != null)
                secondsTimerText.text = string.Format(secondsFormat, hero.RespawnWarningSecondsRemaining);
        }

        private IEnumerator FlashRoutine()
        {
            flashing = true;
            if (warningPanel != null && !warningPanel.activeSelf) warningPanel.SetActive(true);
            if (warningCanvasGroup != null) warningCanvasGroup.alpha = 1f;

            var panelTf = warningPanel != null ? warningPanel.transform : null;
            Vector3 baseScale = panelTf != null ? panelTf.localScale : Vector3.one;
            if (panelTf != null) panelTf.localScale = baseScale * flashScale;

            float t = 0f;
            while (t < flashSeconds)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / flashSeconds);
                // Ease-out: fast decay at the start, settles slow into base scale.
                float eased = 1f - (1f - k) * (1f - k);
                if (panelTf != null)
                    panelTf.localScale = Vector3.LerpUnclamped(baseScale * flashScale, baseScale, eased);
                yield return null;
            }
            if (panelTf != null) panelTf.localScale = baseScale;
            flashing = false;
            flashRoutine = null;
        }

        private void SetPanelActive(bool on)
        {
            if (warningPanel != null && warningPanel.activeSelf != on)
                warningPanel.SetActive(on);
            if (warningCanvasGroup != null) warningCanvasGroup.alpha = on ? 1f : 0f;
        }

        private void PlayBeep()
        {
            if (blinkSfx == null || sfxSource == null) return;
            sfxSource.PlayOneShot(blinkSfx, blinkSfxVolume);
        }
    }
}
