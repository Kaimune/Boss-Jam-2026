using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace BossJam.Dialogue
{
    /// <summary>
    /// Black-screen flavour text with the same input feel as DialogueController:
    /// typewriter reveal per line, Space fast-forwards / advances. No portraits,
    /// no speakers, no audio — just a single TMP_Text on a fade panel.
    ///
    /// Input handling lives outside this asmdef (BossJam.Dialogue doesn't
    /// reference InputSystem). The hosting layer polls for Space and calls
    /// <see cref="RequestAdvance"/>; the controller exposes <see cref="IsRevealing"/>
    /// so the host can decide whether Space fast-forwards or advances.
    ///
    /// Scripts live in Resources/Narration/&lt;name&gt;.json and follow the schema
    /// { "lines": [ "...", "..." ] }.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NarrationController : MonoBehaviour
    {
        [Header("UI bindings")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TMP_Text bodyText;

        [Header("Typewriter")]
        [SerializeField] private float secondsPerChar = 0.04f;
        [Tooltip("Hold each fully-typed line for this long before allowing auto-advance. Space still advances instantly.")]
        [SerializeField] private float interLineHoldSeconds = 0.6f;
        [Tooltip("Multiplier applied to typing speed while Space is held / pressed once during reveal.")]
        [SerializeField] private float fastForwardMultiplier = 8f;

        public bool IsPlaying { get; private set; }
        public bool IsRevealing { get; private set; }
        public event Action Finished;

        private List<string> lines;
        private int index;
        private bool fastForward;
        private bool advanceRequested;

        private void Awake() { HideUi(); }

        /// <summary>
        /// Host (e.g. GameStateController) calls this on Space. If the line is
        /// still typing, this fast-forwards the reveal. Once the line is fully
        /// revealed, the next call advances to the following line.
        /// </summary>
        public void RequestAdvance()
        {
            if (!IsPlaying) return;
            if (IsRevealing) fastForward = true;
            else advanceRequested = true;
        }

        public void Play(string scriptName)
        {
            if (IsPlaying)
            {
                Debug.LogWarning($"{nameof(NarrationController)}: Play while playing — ignoring.");
                return;
            }
            var script = NarrationScriptLoader.Load(scriptName);
            if (script == null || script.lines == null || script.lines.Count == 0)
            {
                Debug.LogWarning($"{nameof(NarrationController)}: script '{scriptName}' missing or empty.");
                Finished?.Invoke();
                return;
            }
            lines = script.lines;
            index = 0;
            fastForward = false;
            advanceRequested = false;
            IsPlaying = true;
            ShowUi();
            StartCoroutine(RunScript());
        }

        private IEnumerator RunScript()
        {
            while (index < lines.Count)
            {
                fastForward = false;
                advanceRequested = false;
                IsRevealing = true;
                yield return TypeLine(lines[index] ?? string.Empty);
                IsRevealing = false;
                yield return WaitForAdvance();
                index++;
            }
            HideUi();
            IsPlaying = false;
            Finished?.Invoke();
        }

        private IEnumerator TypeLine(string text)
        {
            if (bodyText == null) yield break;
            bodyText.text = string.Empty;
            for (int i = 0; i < text.Length; i++)
            {
                if (fastForward || advanceRequested) { bodyText.text = text; yield break; }
                bodyText.text += text[i];
                float delay = fastForward ? secondsPerChar / fastForwardMultiplier : secondsPerChar;
                if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
            }
        }

        // After the line is fully typed: short auto-hold, then wait for Space.
        // advanceRequested can short-circuit the hold so Space always feels responsive.
        private IEnumerator WaitForAdvance()
        {
            float held = 0f;
            while (held < interLineHoldSeconds && !advanceRequested)
            {
                held += Time.unscaledDeltaTime;
                yield return null;
            }
            // After the hold, require an explicit Space (advance request).
            while (!advanceRequested) yield return null;
        }

        private void ShowUi()
        {
            if (canvasGroup != null) { canvasGroup.alpha = 1f; canvasGroup.blocksRaycasts = true; canvasGroup.interactable = true; }
        }

        private void HideUi()
        {
            if (canvasGroup != null) { canvasGroup.alpha = 0f; canvasGroup.blocksRaycasts = false; canvasGroup.interactable = false; }
        }
    }
}
