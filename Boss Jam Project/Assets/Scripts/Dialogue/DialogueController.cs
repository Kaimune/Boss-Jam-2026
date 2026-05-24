using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BossJam.Dialogue
{
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

    [DisallowMultipleComponent]
    public sealed class DialogueController : MonoBehaviour
    {
        [Header("UI bindings")]
        [SerializeField] private TMP_Text dialogueText;
        [SerializeField] private TMP_Text nameplateText;
        [SerializeField] private Image nameplateBackground;
        [SerializeField] private RawImage portraitImage;
        [SerializeField] private CanvasGroup canvasGroup;

        [Tooltip("Full-screen black panel toggled per-speaker. Speakers with useBlackoutBackground=true " +
                 "activate it; others deactivate it.")]
        [SerializeField] private GameObject blackoutPanel;

        [Header("Audio")]
        [SerializeField] private AudioSource letterAudioSource;
        [SerializeField] private bool letterSfxEnabled = true;

        [Header("Speakers")]
        [SerializeField] private List<SpeakerProfile> profiles;

        [Header("Typewriter")]
        [SerializeField] private float secondsPerChar = 0.032f;

        public bool IsPlaying { get; private set; }
        public event Action Finished;

        private DialogueRunner runner;
        private string currentLineText;
        private bool advanceRequested;

        private void Awake() { HideUi(); }

        public void Play(string scriptName)
        {
            if (IsPlaying) { Debug.LogWarning($"{nameof(DialogueController)}: Play while playing — ignoring."); return; }
            var asset = DialogueScriptLoader.Load(scriptName);
            if (asset == null)
            {
                Debug.LogWarning($"{nameof(DialogueController)}: script '{scriptName}' not found.");
                Finished?.Invoke();
                return;
            }
            runner = new DialogueRunner(asset);
            IsPlaying = true;
            ShowUi();
            StartCoroutine(RunScript());
        }

#if UNITY_EDITOR
        /// <summary>
        /// Editor-only: snap the dialogue panel to a fully-typed single line with
        /// the speaker's nameplate + portrait applied. Used by ScenePreviewWindow.
        /// Does not start the typewriter, does not raise Finished, does not flip
        /// IsPlaying. Safe at edit time.
        /// </summary>
        public void PreviewLine(DialogueLine line)
        {
            if (line == null) return;
            ApplySpeakerProfile(line.speaker);
            if (dialogueText != null) dialogueText.text = line.text ?? string.Empty;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;
            }
        }
#endif

        // Press-to-advance: during typing, completes the current line instantly;
        // once the line is fully typed, advances to the next line. Dialogue is
        // intentionally unskippable — there is no way to abort the whole sequence.
        public void RequestAdvance() { advanceRequested = true; }

        private IEnumerator RunScript()
        {
            while (!runner.IsFinished)
            {
                if (runner.SpeakerChangedSincePrevious) ApplySpeakerProfile(runner.Current.speaker);
                currentLineText = runner.Current.text ?? string.Empty;
                yield return TypeLine(runner.Current);
                // Wait for an explicit advance press before moving to the next line.
                advanceRequested = false;
                while (!advanceRequested) yield return null;
                runner.Advance();
            }
            HideUi();
            IsPlaying = false;
            Finished?.Invoke();
        }

        private IEnumerator TypeLine(DialogueLine line)
        {
            if (dialogueText == null) yield break;
            dialogueText.text = string.Empty;
            var profile = ResolveProfile(line.speaker);
            advanceRequested = false;
            for (int i = 0; i < currentLineText.Length; i++)
            {
                if (advanceRequested) { dialogueText.text = currentLineText; yield break; }
                char c = currentLineText[i];
                dialogueText.text += c;
                if (!char.IsWhiteSpace(c) && letterSfxEnabled && profile != null && profile.letterTickClip != null && letterAudioSource != null)
                {
                    letterAudioSource.pitch = 1f + UnityEngine.Random.Range(-profile.pitchJitter, profile.pitchJitter);
                    letterAudioSource.PlayOneShot(profile.letterTickClip);
                }
                if (secondsPerChar > 0f) yield return new WaitForSecondsRealtime(secondsPerChar);
            }
        }

        private void ApplySpeakerProfile(string speakerToken)
        {
            var profile = ResolveProfile(speakerToken);
            if (profile == null) { Debug.LogWarning($"{nameof(DialogueController)}: no profile for '{speakerToken}'."); return; }
            if (nameplateText != null) nameplateText.text = profile.displayName;
            if (nameplateBackground != null) nameplateBackground.color = profile.nameplateColor;
            if (portraitImage != null)
            {
                if (profile.portraitTexture != null)
                {
                    portraitImage.texture = profile.portraitTexture;
                    portraitImage.enabled = true;
                }
                else
                {
                    // No portrait for this speaker (e.g. SYSTEM) — hide so the prior speaker's
                    // headshot doesn't linger behind the blackout.
                    portraitImage.enabled = false;
                }
            }
            if (blackoutPanel != null) blackoutPanel.SetActive(profile.useBlackoutBackground);
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
            if (canvasGroup != null) { canvasGroup.alpha = 1f; canvasGroup.blocksRaycasts = true; canvasGroup.interactable = true; }
        }

        private void HideUi()
        {
            if (canvasGroup != null) { canvasGroup.alpha = 0f; canvasGroup.blocksRaycasts = false; canvasGroup.interactable = false; }
            if (blackoutPanel != null) blackoutPanel.SetActive(false);
            if (portraitImage != null) portraitImage.enabled = true; // restore for next time
        }
    }
}
