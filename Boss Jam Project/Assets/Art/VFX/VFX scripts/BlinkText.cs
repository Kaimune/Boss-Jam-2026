using UnityEngine;
using TMPro;

public class BlinkText : MonoBehaviour
{
    public TextMeshProUGUI textObject;

    public float speed = 2f;
    public float minAlpha = 0.2f;
    public float maxAlpha = 1f;

    void Awake()
    {
        if (textObject == null) textObject = GetComponent<TextMeshProUGUI>();
    }

    void Update()
    {
        if (textObject == null) return;

        Color color = textObject.color;

        color.a = Mathf.Lerp(
            minAlpha,
            maxAlpha,
            (Mathf.Sin(Time.time * speed) + 1f) / 2f
        );

        textObject.color = color;
    }
}