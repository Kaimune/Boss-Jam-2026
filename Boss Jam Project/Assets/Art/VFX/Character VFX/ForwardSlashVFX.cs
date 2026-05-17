using UnityEngine;

public class ForwardSlashVFX : MonoBehaviour
{
    public Renderer slashRenderer;

    [Header("Timing")]
    public float lifetime = 0.18f;
    public float steppedFPS = 8f;

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

    private float timer;
    private bool playing;

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

    private void Update()
    {
        if (!playing)
            return;

        timer += Time.deltaTime;

        float rawT = Mathf.Clamp01(timer / lifetime);

        // Makes animation update in chunks
        float steppedT = Mathf.Floor(rawT * steppedFPS) / steppedFPS;
        steppedT = Mathf.Clamp01(steppedT);

        transform.localScale = Vector3.Lerp(startScale, endScale, steppedT);

        float rot = Mathf.Lerp(rotationKick, -rotationKick, steppedT);
        transform.localRotation = originalRotation * Quaternion.Euler(0f, 0f, rot);

        Color c = startColor;
        c.a = 1f - steppedT;
        mat.color = c;

        if (timer >= lifetime)
        {
            playing = false;
            slashRenderer.enabled = false;

            transform.localScale = originalScale;
            transform.localRotation = originalRotation;
        }
    }

    public void PlaySlash()
    {
        timer = 0f;
        playing = true;

        transform.localScale = startScale;
        transform.localRotation = originalRotation;

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
    }
}