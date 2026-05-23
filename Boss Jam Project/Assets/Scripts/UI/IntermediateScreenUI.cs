using System.Collections;
using BossJam.Difficulty;
using BossJam.Game;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BossJam.UI
{
    /// <summary>
    /// Difficulty card shown between the title screen and the cutscene. On wave 1
    /// the labels populate to the starting tier and input arms immediately. On
    /// wave 2+ the panel enters with the previous wave's tier visible, then plays
    /// a slide+scale punch swapping in the current tier — surfacing what changed.
    /// Press SPACE → controller.AdvanceFromIntermediate() rolls into the cutscene.
    ///
    /// Time is paused during Intermediate, so all animation uses unscaled time.
    /// </summary>
    public class IntermediateScreenUI : MonoBehaviour
    {
        [Header("UI refs")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private TMP_Text promptLabel;

        [Header("Difficulty label")]
        [SerializeField] private RectTransform difficultyAnchor;
        [SerializeField] private CanvasGroup difficultyGroup;
        [Tooltip("Tier name — the big label at the top (e.g. 'Mortal').")]
        [SerializeField] private TMP_Text tierNameLabel;
        [Tooltip("Subtitle — the debuff applied this wave, rendered in parens.")]
        [SerializeField] private TMP_Text subtitleLabel;
        [Tooltip("Tier flavour text — sits below the subtitle.")]
        [SerializeField] private TMP_Text tierDescriptionLabel;

        [Header("Scene refs")]
        [SerializeField] private DifficultyRuntime difficulty;

        [Header("Copy")]
        [SerializeField] private string promptText = "Press SPACE to continue";

        [Header("Transition timing")]
        [SerializeField] private float preTransitionPauseSeconds = 0.2f;
        [SerializeField] private float slidePunchSeconds = 0.55f;
        [Tooltip("Horizontal travel of the slide-out/slide-in, in anchored units.")]
        [SerializeField] private float slideDistance = 220f;
        [Tooltip("Overshoot factor at the peak of the punch (1 = no punch).")]
        [SerializeField] private float punchScale = 1.18f;

        private GameStateController controller;
        private bool inputArmed;

        private void Awake()
        {
            controller = GameStateController.Instance ?? FindFirstObjectByType<GameStateController>();
            if (difficulty == null) difficulty = FindFirstObjectByType<DifficultyRuntime>();

            if (controller == null || panelRoot == null)
            {
                Debug.LogWarning($"{nameof(IntermediateScreenUI)}: missing reference; disabling.", this);
                enabled = false;
                return;
            }

            if (promptLabel != null) promptLabel.text = promptText;

            // Start hidden; OnStateChanged flips us on when Intermediate begins.
            panelRoot.SetActive(controller.State == GameState.Intermediate);
            if (panelRoot.activeSelf) StartCoroutine(RunIntro(GameSession.IsMidRun));
        }

        private void OnEnable()
        {
            if (controller != null) controller.StateChanged += OnStateChanged;
        }

        private void OnDisable()
        {
            if (controller != null) controller.StateChanged -= OnStateChanged;
        }

        private void OnStateChanged(GameState state)
        {
            bool show = state == GameState.Intermediate;
            panelRoot.SetActive(show);
            if (show) StartCoroutine(RunIntro(GameSession.IsMidRun));
            else inputArmed = false;
        }

        private void Update()
        {
            if (!inputArmed || controller == null || controller.State != GameState.Intermediate) return;
            var kb = Keyboard.current;
            if (kb != null && kb.spaceKey.wasPressedThisFrame)
            {
                inputArmed = false;
                controller.AdvanceFromIntermediate();
            }
        }

        private IEnumerator RunIntro(bool midRun)
        {
            inputArmed = false;

            if (difficultyGroup != null) difficultyGroup.alpha = 1f;
            if (difficultyAnchor != null)
            {
                difficultyAnchor.anchoredPosition = new Vector2(0f, difficultyAnchor.anchoredPosition.y);
                difficultyAnchor.localScale = Vector3.one;
            }

            // Wave 1 has nothing to transition from — just show current state.
            // Wave 2+ enters with the previous tier visible, beat, then punch
            // in the new tier so the player sees what changed.
            if (midRun && difficultyAnchor != null && difficultyGroup != null)
            {
                PopulateLabels(showPrevious: true);
                yield return WaitRealtime(preTransitionPauseSeconds);
                yield return PlayTierPunch();
            }
            else
            {
                PopulateLabels(showPrevious: false);
            }

            inputArmed = true;
        }

        private IEnumerator PlayTierPunch()
        {
            float half = slidePunchSeconds * 0.5f;
            float t = 0f;
            // Slide out left + shrink + fade out.
            while (t < half)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / half);
                difficultyAnchor.anchoredPosition = new Vector2(Mathf.Lerp(0f, -slideDistance, k), difficultyAnchor.anchoredPosition.y);
                difficultyAnchor.localScale = Vector3.Lerp(Vector3.one, Vector3.one * 0.85f, k);
                difficultyGroup.alpha = Mathf.Lerp(1f, 0f, k);
                yield return null;
            }

            PopulateLabels(showPrevious: false);

            // Slide in from right with overshoot punch + fade in.
            difficultyAnchor.anchoredPosition = new Vector2(slideDistance, difficultyAnchor.anchoredPosition.y);
            difficultyAnchor.localScale = Vector3.one * 0.85f;
            difficultyGroup.alpha = 0f;

            t = 0f;
            while (t < half)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / half);
                difficultyAnchor.anchoredPosition = new Vector2(Mathf.Lerp(slideDistance, 0f, EaseOutCubic(k)), difficultyAnchor.anchoredPosition.y);
                float scaleK = k < 0.6f
                    ? Mathf.Lerp(0.85f, punchScale, k / 0.6f)
                    : Mathf.Lerp(punchScale, 1f, (k - 0.6f) / 0.4f);
                difficultyAnchor.localScale = Vector3.one * scaleK;
                difficultyGroup.alpha = Mathf.Lerp(0f, 1f, k);
                yield return null;
            }

            difficultyAnchor.anchoredPosition = new Vector2(0f, difficultyAnchor.anchoredPosition.y);
            difficultyAnchor.localScale = Vector3.one;
            difficultyGroup.alpha = 1f;
        }

        private void PopulateLabels(bool showPrevious)
        {
            if (difficulty == null) return;

            string tierName = showPrevious ? difficulty.PreviousTierName : difficulty.CurrentTierName;
            DebuffEntry tierEntry = showPrevious ? difficulty.PreviousTierEntry : difficulty.CurrentTierEntry;
            DebuffEntry debuffEntry = ResolveDebuffEntry(showPrevious);

            if (tierNameLabel != null)
                tierNameLabel.text = tierName ?? string.Empty;

            // Subtitle = the debuff that defines this tier, in parens. Hidden on
            // wave 1 (nothing applied) so the title doesn't float with stray "()".
            if (subtitleLabel != null)
            {
                string debuffName = debuffEntry != null ? debuffEntry.name : null;
                bool hasSubtitle = !string.IsNullOrWhiteSpace(debuffName);
                subtitleLabel.text = hasSubtitle ? $"({debuffName})" : string.Empty;
                subtitleLabel.gameObject.SetActive(hasSubtitle);
            }

            if (tierDescriptionLabel != null)
            {
                string desc = tierEntry != null ? tierEntry.tierDescription : null;
                tierDescriptionLabel.text = desc ?? string.Empty;
                tierDescriptionLabel.gameObject.SetActive(!string.IsNullOrWhiteSpace(desc));
            }
        }

        // Latest applied debuff (or the wave-before-this-one when previewing the
        // previous tier during the punch transition). Wave 1 with nothing applied
        // returns null so the subtitle stays hidden.
        private DebuffEntry ResolveDebuffEntry(bool showPrevious)
        {
            if (difficulty == null) return null;
            var applied = difficulty.Applied;
            int n = applied.Count;
            if (showPrevious) return n >= 2 ? applied[n - 2] : null;
            return n >= 1 ? applied[n - 1] : null;
        }

        private static float EaseOutCubic(float k)
        {
            k = 1f - Mathf.Clamp01(k);
            return 1f - k * k * k;
        }

        private static IEnumerator WaitRealtime(float seconds)
        {
            if (seconds <= 0f) yield break;
            float t = 0f;
            while (t < seconds) { t += Time.unscaledDeltaTime; yield return null; }
        }
    }
}
