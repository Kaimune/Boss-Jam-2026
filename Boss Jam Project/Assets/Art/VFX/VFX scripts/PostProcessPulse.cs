using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public class PostProcessPulse : MonoBehaviour
{
    [Header("Volume")]
    [SerializeField] private Volume effectVolume;

    [Header("Timing")]
    [SerializeField] private float fadeInTime = 0.05f;
    [SerializeField] private float holdTime = 0.5f;
    [SerializeField] private float fadeOutTime = 0.15f;

    private Coroutine routine;

    private void Awake()
    {
        if (effectVolume != null)
            effectVolume.weight = 0f;
    }

    public void PlayEffect()
    {
        if (routine != null)
            StopCoroutine(routine);

        routine = StartCoroutine(EffectRoutine());
    }

    private IEnumerator EffectRoutine()
    {
        // fade in
        yield return TweenWeight(0f, 1f, fadeInTime);

        // hold
        yield return new WaitForSeconds(holdTime);

        // fade out
        yield return TweenWeight(1f, 0f, fadeOutTime);

        routine = null;
    }

    private IEnumerator TweenWeight(float from, float to, float duration)
    {
        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;

            float a = duration <= 0f ? 1f : t / duration;

            effectVolume.weight = Mathf.Lerp(from, to, a);

            yield return null;
        }

        effectVolume.weight = to;
    }
}