using System;
using System.Collections;
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

        public void PlayHeroDeath(int waveIndex)
        {
            StartCoroutine(HeroDeathRoutine(waveIndex));
        }

        public void PlayBossDeath(int waveIndex)
        {
            StartCoroutine(BossDeathRoutine(waveIndex));
        }

        private IEnumerator HeroDeathRoutine(int waveIndex)
        {
            yield return new WaitForSecondsRealtime(heroDeathHoldSeconds);

            yield return PlayLine(string.Format(heroDeathScriptFormat, waveIndex));

            // Fade to black and leave it on — the scene reload happens under
            // the black overlay so the player never sees the dying-wave scene.
            // The next wave's FadeOverlay starts at alpha=1 and the start
            // screen fades it out once the tier transition is wired.
            if (fadeOverlay != null) yield return fadeOverlay.FadeIn(fadeDurationSeconds);

            OutroComplete?.Invoke();
        }

        private IEnumerator BossDeathRoutine(int waveIndex)
        {
            yield return new WaitForSecondsRealtime(heroDeathHoldSeconds);

            string scriptName = waveIndex > 0
                ? string.Format(bossDeathScriptFormat, waveIndex)
                : gameOverScriptFallback;

            yield return PlayLine(scriptName);

            if (fadeOverlay != null) yield return fadeOverlay.FadeIn(fadeDurationSeconds);

            // Leave fade on; GameOverScreenUI shows over it.
            OutroComplete?.Invoke();
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
