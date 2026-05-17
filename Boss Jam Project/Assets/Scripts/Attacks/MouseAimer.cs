using UnityEngine;
using UnityEngine.InputSystem;

namespace BossJam.Attacks
{
    /// <summary>
    /// Reads the mouse cursor and projects it onto the ground plane (Y = groundPlaneY).
    /// Returns the world point where directional attacks should aim.
    /// </summary>
    [DisallowMultipleComponent]
    public class MouseAimer : MonoBehaviour
    {
        [SerializeField] private Camera cam;
        [Tooltip("World-space Y of the ground plane the cursor ray intersects.")]
        [SerializeField] private float groundPlaneY = 0f;

        private void Awake()
        {
            if (cam == null) cam = Camera.main;
        }

        public Vector3 GetAimWorldPoint()
        {
            if (cam == null) cam = Camera.main;
            if (cam == null) return transform.position;

            var screen = Mouse.current != null
                ? Mouse.current.position.ReadValue()
                : (Vector2)Input.mousePosition;
            var ray = cam.ScreenPointToRay(screen);
            var plane = new Plane(Vector3.up, new Vector3(0f, groundPlaneY, 0f));
            return plane.Raycast(ray, out float dist) ? ray.GetPoint(dist) : transform.position;
        }
    }
}
