using System.Collections;
using TMPro;
using UnityEngine;

namespace BossJam.Cutscene
{
    /// <summary>
    /// Central tier label + debuff description. Animated alpha + scale on show.
    /// All waits realtime so it works during the timescale-0 cutscene.
    /// </summary>
    public sealed class TierCardUI : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform root;
        [SerializeField] private TMP_Text labelText;
        [SerializeField] private TMP_Text debuffText;
        [SerializeField] private float fadeInSeconds = 0.38f;
        [SerializeField] private float fadeOutSeconds = 0.38f;
        [SerializeField] private Vector2 startScale = new Vector2(0.96f, 0.96f);

        private void Awake() { Hide(); }

        public void Hide()
        {
            if (canvasGroup != null) canvasGroup.alpha = 0f;
            if (root != null) root.localScale = startScale;
        }

        public IEnumerator Show(string label, string debuff, float holdSeconds)
        {
            if (labelText != null) labelText.text = label;
            if (debuffText != null) debuffText.text = debuff;
            yield return Animate(0f, 1f, startScale, Vector3.one, fadeInSeconds);
            float t = 0f;
            while (t < holdSeconds) { t += Time.unscaledDeltaTime; yield return null; }
            yield return Animate(1f, 0f, Vector3.one, startScale, fadeOutSeconds);
        }

        private IEnumerator Animate(float a0, float a1, Vector3 s0, Vector3 s1, float dur)
        {
            if (dur <= 0f) {
                if (canvasGroup != null) canvasGroup.alpha = a1;
                if (root != null) root.localScale = s1;
                yield break;
            }
            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / dur);
                if (canvasGroup != null) canvasGroup.alpha = Mathf.Lerp(a0, a1, k);
                if (root != null) root.localScale = Vector3.Lerp(s0, s1, k);
                yield return null;
            }
            if (canvasGroup != null) canvasGroup.alpha = a1;
            if (root != null) root.localScale = s1;
        }
    }
}
