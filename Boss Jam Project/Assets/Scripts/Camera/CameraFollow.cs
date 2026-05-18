using UnityEngine;

namespace BossJam.CameraSys
{
    [DisallowMultipleComponent]
    public class CameraFollow : MonoBehaviour
    {
        [Tooltip("Transform to follow. If null, auto-finds the BossController in the scene at Awake.")]
        [SerializeField] private Transform target;

        [Tooltip("World-space offset from the target. Captured from the current transform at Awake when zero.")]
        [SerializeField] private Vector3 offset;

        [Tooltip("Higher = snappier follow. Smooth time for SmoothDamp position lerp.")]
        [SerializeField, Min(0f)] private float smoothTime = 0.15f;

        private Vector3 velocity;

        private void Awake()
        {
            if (target == null)
            {
                var boss = FindFirstObjectByType<BossJam.Player.BossController>();
                if (boss != null) target = boss.transform;
            }
            if (target != null && offset == Vector3.zero)
                offset = transform.position - target.position;
        }

        private void LateUpdate()
        {
            if (target == null) return;
            var desired = target.position + offset;
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime);
        }
    }
}
