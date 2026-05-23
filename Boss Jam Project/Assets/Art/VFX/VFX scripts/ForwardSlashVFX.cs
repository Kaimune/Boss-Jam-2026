using UnityEngine;
using DG.Tweening;

public class ForwardSlashVFX : MonoBehaviour
{
    public Renderer slashRenderer;

    [Header("Timing")]
    public float lifetime = 0.18f;

    [Header("Scale")]
    public Vector3 startScale = new Vector3(0.6f, 0.6f, 1f);
    public Vector3 endScale = new Vector3(1.4f, 1.4f, 1f);

    [Header("Extra Punch")]
    public float rotationKick = 8f;
    public bool randomFlipY = true;

    private Material mat;
    private Color startColor;
    private Vector3 originalScale;
    private Quaternion originalRotation;

    private Sequence sequence;

    private void Awake()
    {
        if (slashRenderer == null)
            slashRenderer = GetComponent<Renderer>();

        mat = slashRenderer.material;
        startColor = mat.color;

        originalScale = transform.localScale;
        originalRotation = transform.localRotation;

        slashRenderer.enabled = false;
    }

    private void OnDestroy()
    {
        sequence?.Kill();

        if (mat != null)
            Destroy(mat);
    }

    public void PlaySlash()
    {
        sequence?.Kill();

        transform.localScale = startScale;
        transform.localRotation = originalRotation * Quaternion.Euler(0f, 0f, rotationKick);

        if (randomFlipY && Random.value > 0.5f)
        {
            Vector3 s = transform.localScale;
            s.y *= -1f;
            transform.localScale = s;
        }

        Color c = startColor;
        c.a = 1f;
        mat.color = c;

        slashRenderer.enabled = true;

        sequence = DOTween.Sequence();

        sequence.Append(
            transform.DOScale(endScale, lifetime)
                .SetEase(Ease.OutQuad)
        );

        sequence.Join(
            transform.DOLocalRotateQuaternion(
                originalRotation * Quaternion.Euler(0f, 0f, -rotationKick),
                lifetime
            ).SetEase(Ease.OutQuad)
        );

        sequence.Join(
            mat.DOFade(0f, lifetime)
                .SetEase(Ease.Linear)
        );

        sequence.OnComplete(() =>
        {
            slashRenderer.enabled = false;
            transform.localScale = originalScale;
            transform.localRotation = originalRotation;

            Color reset = startColor;
            reset.a = startColor.a;
            mat.color = reset;
        });
    }
}