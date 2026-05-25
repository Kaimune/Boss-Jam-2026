using UnityEngine;

namespace BossJam.Visuals
{
    /// <summary>
    /// Spins in stepped/framerate-limited increments.
    /// Retro PS1 / sprite-like motion.
    /// </summary>
    public sealed class Spinner : MonoBehaviour
    {
        [SerializeField] private Vector3 rotationAxis = new Vector3(0f, 1f, 0f);
        [SerializeField] private float spinSpeed = 720f;

        [Header("Retro Stepping")]
        [SerializeField] private int stepFps = 12;

        private float timer;

        private void Update()
        {
            timer += Time.deltaTime;

            float stepTime = 1f / stepFps;

            if (timer < stepTime)
                return;

            timer -= stepTime;

            transform.Rotate(rotationAxis * spinSpeed * stepTime);
        }
    }
}