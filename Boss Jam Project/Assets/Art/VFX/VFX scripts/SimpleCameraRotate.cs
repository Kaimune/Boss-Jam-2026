using UnityEngine;
using UnityEngine.InputSystem;

public class SimpleCameraRotate : MonoBehaviour
{
    public float rotationSpeed = 100f;

    void Update()
    {
        float scroll = Mouse.current.scroll.ReadValue().y;

        transform.Rotate(
            0f,
            scroll * rotationSpeed * Time.deltaTime,
            0f,
            Space.World
        );
    }
}