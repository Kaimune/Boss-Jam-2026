using System;
using System.Collections;
using BossJam.Audio;
using BossJam.Game;
using UnityEngine;

namespace BossJam.Cutscene
{
    /// <summary>
    /// Drives the death cutscenes.
    ///   PlayHeroDeath: pause -> boss line -> fade to black -> OutroComplete.
    ///     Fade stays on; the controller reloads the scene under black and
    ///     the next wave's StartScreenUI owns the fade-out + tier transition.
    ///   PlayBossDeath: pause -> defeat line -> fade in -> OutroComplete (fade stays on,
    ///     GameOverScreenUI shows over the faded canvas).
    /// </summary>
    public sealed class OutroDirector : MonoBehaviour
    {
        [SerializeField] private DialogueRig dialogueRig;
        [SerializeField] private FadeOverlay fadeOverlay;

        [SerializeField] private string heroDeathScriptFormat = "hero_death_wave_{0}";
        [SerializeField] private string bossDeathScriptFormat = "boss_death_wave_{0}";
        [SerializeField] private string gameOverScriptFallback = "game_over";
        [SerializeField] private float heroDeathHoldSeconds = 0.4f;
        [SerializeField] private float fadeDurationSeconds = 0.6f;

        public event Action OutroComplete;

        public void PlayHeroDeath(int waveIndex, DeathFx victim = null)
        {
            StartCoroutine(HeroDeathRoutine(waveIndex, victim));
        }

        public void PlayBossDeath(int waveIndex, DeathFx victim = null)
        {
            StartCoroutine(BossDeathRoutine(waveIndex, victim));
        }

        private IEnumerator HeroDeathRoutine(int waveIndex, DeathFx victim)
        {
            // DeathFx.Play() was already fired at the kill site (HeroEnemy /
            // BossController). Here we just wait long enough for the clip to
            // complete before the dialogue cue starts.
            float wait = (victim != null && victim.ClipLengthSeconds > 0f)
                ? victim.ClipLengthSeconds
                : heroDeathHoldSeconds;
            yield return new WaitForSecondsRealtime(wait);

            yield return PlayLine(string.Format(heroDeathScriptFormat, waveIndex));

            // Freeze the world for the fade so in-flight fireballs / hitboxes
            // don't visibly drift behind the partially-transparent overlay.
            // Fade/typewriter use unscaled time; ReloadScene restores timescale
            // before the next scene loads.
            Time.timeScale = 0f;

            // Fade to black and leave it on — the scene reload happens under
            // the black overlay so the player never sees the dying-wave scene.
            // The next wave's FadeOverlay starts at alpha=1 and the start
            // screen fades it out once the tier transition is wired.
            if (fadeOverlay != null) yield return fadeOverlay.FadeIn(fadeDurationSeconds);

            OutroComplete?.Invoke();
        }

        private IEnumerator BossDeathRoutine(int waveIndex, DeathFx victim)
        {
            float wait = (victim != null && victim.ClipLengthSeconds > 0f)
                ? victim.ClipLengthSeconds
                : heroDeathHoldSeconds;
            yield return new WaitForSecondsRealtime(wait);

            string scriptName = waveIndex > 0
                ? string.Format(bossDeathScriptFormat, waveIndex)
                : gameOverScriptFallback;

            // Fire OutroComplete the SAME frame the dialogue's Finished event
            // hits — no extra-frame wait. The controller restores save-scum
            // and reloads the scene synchronously so the world can't keep
            // running between the last Space press and the reload.
            if (dialogueRig != null && dialogueRig.Controller != null)
            {
                bool fired = false;
                Action onFinished = null;
                onFinished = () =>
                {
                    if (fired) return;
                    fired = true;
                    dialogueRig.Controller.Finished -= onFinished;
                    Time.timeScale = 0f;
                    if (fadeOverlay != null) fadeOverlay.SetAlpha(1f);
                    OutroComplete?.Invoke();
                };
                dialogueRig.Controller.Finished += onFinished;
                dialogueRig.Play(scriptName);
            }
            else
            {
                Time.timeScale = 0f;
                if (fadeOverlay != null) fadeOverlay.SetAlpha(1f);
                OutroComplete?.Invoke();
            }
        }

        private IEnumerator PlayLine(string scriptName)
        {
            if (dialogueRig == null || dialogueRig.Controller == null) yield break;
            bool done = false;
            Action handler = () => done = true;
            dialogueRig.Controller.Finished += handler;
            dialogueRig.Play(scriptName);
            while (!done) yield return null;
            dialogueRig.Controller.Finished -= handler;
        }
    }
}
