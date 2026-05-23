using UnityEngine;
using DG.Tweening;

public class SlamEchoVFX : MonoBehaviour
{
    public GameObject trianglePrefab;

    [Header("Shape")]
    public int triangleCount = 10;
    public float startRadius = 0.15f;
    public float endRadius = 1.2f;

    [Header("Timing")]
    public float duration = 0.18f;
    public float steppedFPS = 8f;

    [Header("Scale")]
    public float startScaleMultiplier = 0.8f;
    public float endScaleMultiplier = 1.4f;

    [Header("Visual")]
    public float heightOffset = 0.02f;

    private Renderer sourceRenderer;

    private void Awake()
    {
        sourceRenderer = GetComponentInChildren<Renderer>();

        if (sourceRenderer != null)
            sourceRenderer.enabled = false;
    }

    public void PlayEcho()
    {
        SpawnRing();
    }

    private void SpawnRing()
    {
        if (trianglePrefab == null)
            return;

        for (int i = 0; i < triangleCount; i++)
        {
            float angle = ((float)i / triangleCount) * 360f;
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;

            GameObject tri = Instantiate(
                trianglePrefab,
                transform.position + dir * startRadius + Vector3.up * heightOffset,
                Quaternion.Euler(0f, angle, 0f)
            );

            Renderer r = tri.GetComponentInChildren<Renderer>();
            if (r != null)
                r.enabled = true;

            Material mat = null;
            if (r != null)
                mat = r.material;

            Vector3 baseScale = tri.transform.localScale;

            tri.transform.position =
                transform.position + dir * startRadius + Vector3.up * heightOffset;

            tri.transform.localScale = baseScale * startScaleMultiplier;

            if (mat != null)
            {
                Color c = mat.color;
                c.a = 1f;
                mat.color = c;
            }

            float tweenValue = 0f;

            DOTween.To(
                () => tweenValue,
                x =>
                {
                    tweenValue = x;

                    float stepped = Mathf.Floor(tweenValue * steppedFPS) / steppedFPS;
                    stepped = Mathf.Clamp01(stepped);

                    tri.transform.position =
                        transform.position +
                        dir * Mathf.Lerp(startRadius, endRadius, stepped) +
                        Vector3.up * heightOffset;

                    tri.transform.localScale =
                        Vector3.Lerp(
                            baseScale * startScaleMultiplier,
                            baseScale * endScaleMultiplier,
                            stepped
                        );

                    if (mat != null)
                    {
                        Color c = mat.color;
                        c.a = 1f - stepped;
                        mat.color = c;
                    }
                },
                1f,
                duration
            )
            .SetEase(Ease.Linear)
            .OnComplete(() =>
            {
                Destroy(tri);
            });
        }
    }
}