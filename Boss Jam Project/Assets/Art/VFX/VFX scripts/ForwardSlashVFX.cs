using UnityEngine;
using DG.Tweening;

public class ForwardSlashVFX : MonoBehaviour
{
    public Renderer slashRenderer;

    [Header("Timing")]
    public float lifetime = 0.18f;
    public float steppedFPS = 6f;

    [Header("Scale")]
    public Vector3 startScale = new Vector3(0.6f, 0.6f, 1f);
    public Vector3 endScale = new Vector3(1.4f, 1.4f, 1f);

    [Header("Extra Punch")]
    public float rotationKick = 8f;
    public bool randomFlipY = true;

    private Material mat;
    private Color startColor;

    private Transform originalParent;
    private Vector3 originalLocalPosition;
    private Quaternion originalLocalRotation;
    private Vector3 originalLocalScale;

    private Tween activeTween;

    private void Awake()
    {
        if (slashRenderer == null)
            slashRenderer = GetComponent<Renderer>();

        mat = slashRenderer.material;
        startColor = mat.color;

        originalParent = transform.parent;
        originalLocalPosition = transform.localPosition;
        originalLocalRotation = transform.localRotation;
        originalLocalScale = transform.localScale;

        slashRenderer.enabled = false;
    }

    public void PlaySlash()
    {
        if (activeTween != null)
            activeTween.Kill();

        Vector3 worldPos = transform.position;
        Quaternion worldRot = transform.rotation;

        transform.SetParent(null, true);
        transform.position = worldPos;
        transform.rotation = worldRot;

        slashRenderer.enabled = true;

        Vector3 initialScale = startScale;

        if (randomFlipY && Random.value > 0.5f)
        {
            initialScale.y *= -1f;
        }

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
                    Vector3.Lerp(initialScale, endScale, stepped);

                float rot =
                    Mathf.Lerp(rotationKick, -rotationKick, stepped);

                transform.localRotation =
                    worldRot * Quaternion.Euler(0f, 0f, rot);

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
            slashRenderer.enabled = false;

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
