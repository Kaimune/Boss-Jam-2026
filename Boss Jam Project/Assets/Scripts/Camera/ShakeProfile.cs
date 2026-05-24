using UnityEngine;

namespace BossJam.CameraSys
{
    /// <summary>
    /// Tunable one-shot camera shake. Drop into an AnimationEvent's Object slot
    /// and trigger via <see cref="AnimationShake.Play"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Boss Jam/Camera/Shake Profile", fileName = "shake_")]
    public sealed class ShakeProfile : ScriptableObject
    {
        [Tooltip("Peak offset in world units along the camera's local right/up axes.")]
        [Min(0f)] public float amplitude = 0.15f;

        [Tooltip("How long the shake takes to fully decay, in seconds.")]
        [Min(0f)] public float duration = 0.25f;

        [Tooltip("Jitter rate in Hz. Higher = faster wiggle. Perlin noise is sampled at this rate.")]
        [Min(0f)] public float frequency = 25f;

        [Tooltip("Envelope: x=0..1 normalized time, y=0..1 amplitude scalar. Default eases out from 1 to 0.")]
        public AnimationCurve decayCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
    }
}
