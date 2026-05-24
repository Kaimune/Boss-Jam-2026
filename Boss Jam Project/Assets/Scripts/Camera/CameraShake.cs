using System.Collections.Generic;
using UnityEngine;

namespace BossJam.CameraSys
{
    /// <summary>
    /// Layers an additive camera-local offset on top of <see cref="CameraFollow"/>
    /// without touching CameraFollow itself. Update (which always runs before any
    /// LateUpdate) restores the un-shaken base position; CameraFollow.LateUpdate
    /// then SmoothDamps on a clean base; this component's LateUpdate
    /// (DefaultExecutionOrder=100, so after CameraFollow) re-adds the freshly
    /// computed shake offset.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public sealed class CameraShake : MonoBehaviour
    {
        private static CameraShake _instance;

        private struct Impulse
        {
            public ShakeProfile profile;
            public float startTime;
            public float seedX;
            public float seedY;
        }

        private readonly List<Impulse> _active = new List<Impulse>(8);
        private Vector3 _lastOffset;

        public static void Impulse(ShakeProfile profile)
        {
            if (_instance == null || profile == null) return;
            _instance._active.Add(new Impulse
            {
                profile = profile,
                startTime = Time.time,
                seedX = Random.value * 1000f,
                seedY = Random.value * 1000f,
            });
        }

        private void OnEnable()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning($"[CameraShake] Multiple instances detected; '{_instance.name}' is being replaced by '{name}'.", this);
            }
            _instance = this;
        }

        private void OnDisable()
        {
            if (_instance == this) _instance = null;
            _active.Clear();
            _lastOffset = Vector3.zero;
        }

        private void Update()
        {
            if (_lastOffset != Vector3.zero)
            {
                transform.position -= _lastOffset;
                _lastOffset = Vector3.zero;
            }
        }

        private void LateUpdate()
        {
            if (_active.Count == 0) return;

            var offset = Vector2.zero;
            var now = Time.time;

            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var imp = _active[i];
                var dur = Mathf.Max(imp.profile.duration, 1e-4f);
                var t = (now - imp.startTime) / dur;

                if (t >= 1f)
                {
                    _active.RemoveAt(i);
                    continue;
                }

                var env = imp.profile.decayCurve.Evaluate(t) * imp.profile.amplitude;
                var sample = now * imp.profile.frequency;
                var nx = (Mathf.PerlinNoise(imp.seedX + sample, 0f) - 0.5f) * 2f;
                var ny = (Mathf.PerlinNoise(imp.seedY + sample, 0f) - 0.5f) * 2f;
                offset.x += nx * env;
                offset.y += ny * env;
            }

            var worldOffset = transform.right * offset.x + transform.up * offset.y;
            transform.position += worldOffset;
            _lastOffset = worldOffset;
        }
    }
}
