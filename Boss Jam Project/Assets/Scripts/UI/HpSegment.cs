using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace BossJam.UI
{
    public class HpSegment : MonoBehaviour
    {
        [SerializeField] private GameObject emptyOverlay;
        [SerializeField] private Image flashOverlay;
        [SerializeField, Min(0f)] private float flashDuration = 0.15f;

        private Coroutine flashRoutine;

        public void SetFilled(bool filled)
        {
            if (emptyOverlay != null) emptyOverlay.SetActive(!filled);
        }

        public void Flash()
        {
            if (flashOverlay == null) return;
            if (flashRoutine != null) StopCoroutine(flashRoutine);
            flashRoutine = StartCoroutine(FlashRoutine());
        }

        private IEnumerator FlashRoutine()
        {
            var c = flashOverlay.color;
            float t = 0f;
            c.a = 1f;
            flashOverlay.color = c;
            while (t < flashDuration)
            {
                t += Time.deltaTime;
                c.a = Mathf.Clamp01(1f - t / flashDuration);
                flashOverlay.color = c;
                yield return null;
            }
            c.a = 0f;
            flashOverlay.color = c;
            flashRoutine = null;
        }
    }
}
