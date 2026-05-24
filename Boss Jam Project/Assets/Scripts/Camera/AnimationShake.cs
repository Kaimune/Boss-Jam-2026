using UnityEngine;

namespace BossJam.CameraSys
{
    /// <summary>
    /// Bridge between Animation Events and <see cref="CameraShake"/>. Attach to
    /// the same GameObject as the Animator. Author a keyframe in the Animation
    /// window, set Function = <c>Play</c>, and drop a <see cref="ShakeProfile"/>
    /// into the event's Object slot — the shake fires through the static
    /// CameraShake instance at that frame.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AnimationShake : MonoBehaviour
    {
        /// <summary>Animation Event entry point. Reads the profile from the event's Object slot.</summary>
        public void Play(AnimationEvent ev)
        {
            if (!Application.isPlaying) return;
            if (ev == null) return;
            CameraShake.Impulse(ev.objectReferenceParameter as ShakeProfile);
        }

        /// <summary>Alternate entry point for direct ShakeProfile-only events (no AnimationEvent wrapper).</summary>
        public void PlayProfile(ShakeProfile profile)
        {
            if (!Application.isPlaying) return;
            CameraShake.Impulse(profile);
        }
    }
}
