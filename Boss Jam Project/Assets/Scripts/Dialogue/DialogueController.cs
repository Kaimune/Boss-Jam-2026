using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BossJam.Dialogue
{
    /// <summary>
    /// Plain-data iterator over a DialogueScriptAsset. Lives in this file so
    /// the controller and its EditMode tests share the same definition.
    /// </summary>
    public sealed class DialogueRunner
    {
        private readonly List<DialogueLine> lines;
        private int index;
        private string previousSpeaker;

        public DialogueRunner(DialogueScriptAsset asset)
        {
            lines = asset != null && asset.lines != null ? asset.lines : new List<DialogueLine>();
            index = 0;
            previousSpeaker = null;
        }

        public bool IsFinished => index >= lines.Count;
        public DialogueLine Current => IsFinished ? null : lines[index];

        public bool SpeakerChangedSincePrevious =>
            !IsFinished && (previousSpeaker == null || previousSpeaker != Current.speaker);

        public void Advance()
        {
            if (IsFinished) return;
            previousSpeaker = Current.speaker;
            index++;
        }
    }

    /// <summary>
    /// Drives the dialogue UI in GameplayScene. Loads a script by name, walks
    /// lines, types text character-by-character into a TMP_Text, plays letter
    /// ticks, and swaps the portrait RawImage's texture on speaker change.
    /// Other systems pause gameplay around Play(...) — this script doesn't
    /// touch Time.timeScale or BossController.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DialogueController : MonoBehaviour
    {
        [Header("UI bindings")]
        [SerializeField] private TMP_Text dialogueText;
        [SerializeField] private TMP_Text nameplateText;
        [SerializeField] private Image nameplateBackground;
        [SerializeField] private RawImage portraitImage;
        [SerializeField] private GameObject advanceIndicator;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Audio")]
        [SerializeField] private AudioSource letterAudioSource;
        [SerializeField] private bool letterSfxEnabled = true;

        [Header("Speakers")]
        [SerializeField] private List<SpeakerProfile> profiles;

        [Header("Typewriter")]
        [Tooltip("Seconds per character.")]
        [SerializeField] private float secondsPerChar = 0.032f;

        public bool IsPlaying { get; private set; }
        public event Action Finished;

        private DialogueRunner runner;
        private Coroutine typingRoutine;
        private bool skipRequested;
        private bool completeLineRequested;
        private string currentLineText;

        private void Awake()
        {
            HideUi();
        }

        public void Play(string scriptName)
        {
            if (IsPlaying)
            {
                Debug.LogWarning($"{nameof(DialogueController)}: Play called while already playing — ignoring.");
                return;
            }
            var asset = DialogueScriptLoader.Load(scriptName);
            if (asset == null)
            {
                Debug.LogWarning($"{nameof(DialogueController)}: script '{scriptName}' not found. Skipping dialogue.");
                Finished?.Invoke();
                return;
            }
            runner = new DialogueRunner(asset);
            IsPlaying = true;
            ShowUi();
            typingRoutine = StartCoroutine(RunScript());
        }

        public void RequestAdvance()
        {
            if (!IsPlaying) return;
            completeLineRequested = true;
        }

        public void RequestSkip()
        {
            skipRequested = true;
            completeLineRequested = true;
        }

        private IEnumerator RunScript()
        {
            while (!runner.IsFinished)
            {
                if (runner.SpeakerChangedSincePrevious) ApplySpeakerProfile(runner.Current.speaker);
                currentLineText = runner.Current.text ?? string.Empty;
                yield return TypeLine(runner.Current);

                if (advanceIndicator != null) advanceIndicator.SetActive(true);
                completeLineRequested = false;
                while (!completeLineRequested) yield return null;
                if (advanceIndicator != null) advanceIndicator.SetActive(false);
                completeLineRequested = false;

                runner.Advance();
            }
            HideUi();
            IsPlaying = false;
            typingRoutine = null;
            Finished?.Invoke();
        }

        private IEnumerator TypeLine(DialogueLine line)
        {
            if (advanceIndicator != null) advanceIndicator.SetActive(false);
            if (dialogueText == null) yield break;
            dialogueText.text = string.Empty;
            completeLineRequested = false;
            var profile = ResolveProfile(line.speaker);
            for (int i = 0; i < currentLineText.Length; i++)
            {
                if (completeLineRequested)
                {
                    dialogueText.text = currentLineText;
                    completeLineRequested = false;
                    break;
                }
                char c = currentLineText[i];
                dialogueText.text += c;
                if (!char.IsWhiteSpace(c) && letterSfxEnabled && profile != null && profile.letterTickClip != null && letterAudioSource != null)
                {
                    letterAudioSource.pitch = 1f + UnityEngine.Random.Range(-profile.pitchJitter, profile.pitchJitter);
                    letterAudioSource.PlayOneShot(profile.letterTickClip);
                }
                yield return new WaitForSecondsRealtime(secondsPerChar);
                if (skipRequested) { dialogueText.text = currentLineText; skipRequested = false; break; }
            }
        }

        private void ApplySpeakerProfile(string speakerToken)
        {
            var profile = ResolveProfile(speakerToken);
            if (profile == null)
            {
                Debug.LogWarning($"{nameof(DialogueController)}: no SpeakerProfile for token '{speakerToken}'.");
                return;
            }
            if (nameplateText != null) nameplateText.text = profile.displayName;
            if (nameplateBackground != null) nameplateBackground.color = profile.nameplateColor;
            if (portraitImage != null && profile.portraitTexture != null) portraitImage.texture = profile.portraitTexture;
        }

        private SpeakerProfile ResolveProfile(string token)
        {
            if (string.IsNullOrEmpty(token) || profiles == null) return null;
            for (int i = 0; i < profiles.Count; i++)
                if (profiles[i] != null && profiles[i].speakerToken == token) return profiles[i];
            return null;
        }

        private void ShowUi()
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.blocksRaycasts = true;
                canvasGroup.interactable = true;
            }
            gameObject.SetActive(true);
        }

        private void HideUi()
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;
            }
            if (advanceIndicator != null) advanceIndicator.SetActive(false);
        }
    }
}
