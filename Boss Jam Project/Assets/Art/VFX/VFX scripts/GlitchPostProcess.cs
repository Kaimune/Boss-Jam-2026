using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public class GlitchPostProcess : MonoBehaviour
{
    [Header("Volume")]
    [SerializeField] private Volume glitchVolume;

    [Header("Timing")]
    [SerializeField] private float transitionTime = 0.4f;

    private Coroutine routine;

    private void Awake()
    {
        if (glitchVolume != null)
            glitchVolume.weight = 0f;
    }

    public void EnableGlitch()
    {
        TweenTo(1f);
    }

    public void DisableGlitch()
    {
        TweenTo(0f);
    }

    private void TweenTo(float target)
    {
        if (glitchVolume == null)
            return;

        if (routine != null)
            StopCoroutine(routine);

        routine = StartCoroutine(TweenRoutine(target));
    }

    private IEnumerator TweenRoutine(float target)
    {
        float start = glitchVolume.weight;

        float t = 0f;

        while (t < transitionTime)
        {
            t += Time.deltaTime;

            float a = transitionTime <= 0f
                ? 1f
                : t / transitionTime;

            // smoother feel
            a = Mathf.SmoothStep(0f, 1f, a);

            glitchVolume.weight = Mathf.Lerp(start, target, a);

            yield return null;
        }

        glitchVolume.weight = target;
        routine = null;
    }
}