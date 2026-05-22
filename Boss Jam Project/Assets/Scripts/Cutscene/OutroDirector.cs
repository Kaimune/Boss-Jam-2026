using System;
using System.Collections;
using BossJam.Difficulty;
using BossJam.Game;
using UnityEngine;

namespace BossJam.Cutscene
{
    /// <summary>
    /// Drives the death cutscenes.
    ///   PlayHeroDeath: pause -> boss line -> fade -> tier card -> fade back -> OutroComplete.
    ///   PlayBossDeath: pause -> defeat line -> fade in -> OutroComplete (fade stays on,
    ///     GameOverScreenUI shows over the faded canvas).
    /// </summary>
    public sealed class OutroDirector : MonoBehaviour
    {
        [SerializeField] private DialogueRig dialogueRig;
        [SerializeField] private FadeOverlay fadeOverlay;
        [SerializeField] private TierCardUI tierCardUI;
        [SerializeField] private DifficultyRuntime difficulty;

        [SerializeField] private string heroDeathScriptFormat = "hero_death_wave_{0}";
        [SerializeField] private string gameOverScript = "game_over";
        [SerializeField] private float heroDeathHoldSeconds = 0.4f;
        [SerializeField] private float fadeDurationSeconds = 0.6f;
        [SerializeField] private float tierCardHoldSeconds = 1.8f;

        public event Action OutroComplete;

        private void Awake()
        {
            if (difficulty == null) difficulty = FindFirstObjectByType<DifficultyRuntime>();
        }

        public void PlayHeroDeath(int waveIndex)
        {
            StartCoroutine(HeroDeathRoutine(waveIndex));
        }

        public void PlayBossDeath()
        {
            StartCoroutine(BossDeathRoutine());
        }

        private IEnumerator HeroDeathRoutine(int waveIndex)
        {
            yield return new WaitForSecondsRealtime(heroDeathHoldSeconds);

            yield return PlayLine(string.Format(heroDeathScriptFormat, waveIndex));

            if (fadeOverlay != null) yield return fadeOverlay.FadeIn(fadeDurationSeconds);

            if (tierCardUI != null && difficulty != null)
                yield return tierCardUI.Show(difficulty.NextTierLabel, difficulty.NextDebuffDescription, tierCardHoldSeconds);

            if (fadeOverlay != null) yield return fadeOverlay.FadeOut(fadeDurationSeconds);

            OutroComplete?.Invoke();
        }

        private IEnumerator BossDeathRoutine()
        {
            yield return new WaitForSecondsRealtime(heroDeathHoldSeconds);

            yield return PlayLine(gameOverScript);

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
