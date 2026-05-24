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
    public float steppedFPS = 6f;

    [Header("Scale")]
    public float startScaleMultiplier = 0.8f;
    public float endScaleMultiplier = 1.4f;

    [Header("Visual")]
    public float heightOffset = 0.02f;

    public void PlayEcho()
    {
        SpawnRing();
    }

    private void SpawnRing()
    {
        for (int i = 0; i < triangleCount; i++)
        {
            float angle = ((float)i / triangleCount) * 360f;

            Vector3 dir =
                Quaternion.Euler(0f, angle, 0f) * Vector3.forward;

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

            Vector3 startPos =
                transform.position + dir * startRadius + Vector3.up * heightOffset;

            Vector3 endPos =
                transform.position + dir * endRadius + Vector3.up * heightOffset;

            float tweenValue = 0f;

            DOTween.To(
                () => tweenValue,
                x =>
                {
                    tweenValue = x;

                    float stepped =
                        Mathf.Floor(tweenValue * steppedFPS) / steppedFPS;

                    stepped = Mathf.Clamp01(stepped);

                    tri.transform.position =
                        Vector3.Lerp(startPos, endPos, stepped);

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
                if (r != null)
                    r.enabled = false;

                Destroy(tri);
            });
        }
    }
}
