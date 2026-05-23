using UnityEngine;

public class SlamVFX : MonoBehaviour
{
    public Renderer slamRenderer;

    public float lifetime = 0.22f;
    public float steppedFPS = 8f;

    public Vector3 startScale = new Vector3(0.4f, 0.4f, 1f);
    public Vector3 endScale = new Vector3(3f, 3f, 1f);

    public float heightOffset = 0.03f;

    private Material mat;
    private Color startColor;
    private Vector3 originalScale;
    private Quaternion originalRotation;

    private float timer;
    private bool playing;

    private void Awake()
    {
        if (slamRenderer == null)
            slamRenderer = GetComponent<Renderer>();

        mat = slamRenderer.material;
        startColor = mat.color;

        originalScale = transform.localScale;
        originalRotation = transform.localRotation;

        slamRenderer.enabled = false;
    }

    private void Update()
    {
        if (!playing)
            return;

        timer += Time.deltaTime;

        float rawT = Mathf.Clamp01(timer / lifetime);
        float steppedT = Mathf.Floor(rawT * steppedFPS) / steppedFPS;

        transform.localScale = Vector3.Lerp(startScale, endScale, steppedT);

        Color c = startColor;
        c.a = 1f - steppedT;
        mat.color = c;

        if (timer >= lifetime)
        {
            playing = false;
            slamRenderer.enabled = false;
            transform.localScale = originalScale;
            transform.localRotation = originalRotation;
        }
    }

    public void PlaySlam()
    {
        timer = 0f;
        playing = true;

        transform.localScale = startScale;
        transform.localRotation = originalRotation;

        Vector3 p = transform.localPosition;
        p.y = heightOffset;
        transform.localPosition = p;

        Color c = startColor;
        c.a = 1f;
        mat.color = c;

        slamRenderer.enabled = true;
    }
}