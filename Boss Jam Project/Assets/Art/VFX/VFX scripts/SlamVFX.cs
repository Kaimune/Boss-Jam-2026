using UnityEngine;
using DG.Tweening;

public class SlamVFX : MonoBehaviour
{
    public Renderer slamRenderer;

    [Header("Timing")]
    public float lifetime = 0.22f;
    public float steppedFPS = 6f;

    [Header("Scale")]
    public Vector3 startScale = new Vector3(0.4f, 0.4f, 1f);
    public Vector3 endScale = new Vector3(3f, 3f, 1f);

    [Header("Position")]
    public float heightOffset = 0.03f;

    private Material mat;
    private Color startColor;

    private Transform originalParent;
    private Vector3 originalLocalPosition;
    private Quaternion originalLocalRotation;
    private Vector3 originalLocalScale;

    private Tween activeTween;

    private void Awake()
    {
        if (slamRenderer == null)
            slamRenderer = GetComponent<Renderer>();

        mat = slamRenderer.material;
        startColor = mat.color;

        originalParent = transform.parent;
        originalLocalPosition = transform.localPosition;
        originalLocalRotation = transform.localRotation;
        originalLocalScale = transform.localScale;

        slamRenderer.enabled = false;
    }

    public void PlaySlam()
    {
        if (activeTween != null)
            activeTween.Kill();

        Vector3 worldPos = transform.position;
        Quaternion worldRot = transform.rotation;

        transform.SetParent(null, true);
        transform.position = worldPos + Vector3.up * heightOffset;
        transform.rotation = worldRot;

        slamRenderer.enabled = true;

        float tweenValue = 0f;

        activeTween = DOTween.To(
            () => tweenValue,
            x =>
            {
                tweenValue = x;

                float stepped =
                    Mathf.Floor(tweenValue * steppedFPS) / steppedFPS;

                stepped = Mathf.Clamp01(stepped);

                transform.localScale =
                    Vector3.Lerp(startScale, endScale, stepped);

                Color c = startColor;
                c.a = 1f - stepped;
                mat.color = c;
            },
            1f,
            lifetime
        )
        .SetEase(Ease.Linear)
        .OnComplete(() =>
        {
            slamRenderer.enabled = false;

            transform.SetParent(originalParent, false);
            transform.localPosition = originalLocalPosition;
            transform.localRotation = originalLocalRotation;
            transform.localScale = originalLocalScale;
        });
    }

    private void OnDestroy()
    {
        if (mat != null)
            Destroy(mat);
    }
}
