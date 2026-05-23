using UnityEngine;

public class ScaleParticleWithCamera : MonoBehaviour
{
    public Camera targetCamera;

    [Header("Scaling")]
    public float sizeMultiplier = 0.15f;

    private ParticleSystem ps;
    private ParticleSystem.MainModule main;

    private void Awake()
    {
        ps = GetComponent<ParticleSystem>();
        main = ps.main;

        if (targetCamera == null)
            targetCamera = Camera.main;
    }

    private void Update()
    {
        if (targetCamera == null)
            return;

        main.startSizeMultiplier =
            targetCamera.orthographicSize * sizeMultiplier;
    }
}