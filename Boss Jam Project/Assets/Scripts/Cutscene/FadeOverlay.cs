using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace BossJam.Cutscene
{
    /// <summary>
    /// Full-screen black image with realtime-coroutine fades. Sits on a top-most
    /// canvas above everything. All waits are unscaled so fades work under
    /// Time.timeScale = 0.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public sealed class FadeOverlay : MonoBehaviour
    {
        [SerializeField] private Image image;

        private void Reset() { image = GetComponent<Image>(); }
        private void Awake() { if (image == null) image = GetComponent<Image>(); SetAlpha(0f); }

        public void SetAlpha(float a)
        {
            if (image == null) return;
            var c = image.color; c.a = Mathf.Clamp01(a); image.color = c;
            image.raycastTarget = c.a > 0f;
        }

        public IEnumerator FadeIn(float duration)  { yield return Fade(0f, 1f, duration); }
        public IEnumerator FadeOut(float duration) { yield return Fade(1f, 0f, duration); }

        private IEnumerator Fade(float from, float to, float duration)
        {
            if (duration <= 0f) { SetAlpha(to); yield break; }
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                SetAlpha(Mathf.Lerp(from, to, Mathf.Clamp01(t / duration)));
                yield return null;
            }
            SetAlpha(to);
        }
    }
}
