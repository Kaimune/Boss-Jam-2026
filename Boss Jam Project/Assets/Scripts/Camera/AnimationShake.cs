using UnityEngine;
using UnityEngine.Scripting;

namespace BossJam.CameraSys
{
    /// <summary>
    /// Bridge between Animation Events and <see cref="CameraShake"/>. Attach to
    /// the same GameObject as the Animator. Author a keyframe in the Animation
    /// window, set Function = <c>Play</c>, and drop a <see cref="ShakeProfile"/>
    /// into the event's Object slot — the shake fires through the static
    /// CameraShake instance at that frame.
    /// </summary>
    // [Preserve] blocks IL2CPP from stripping these methods on WebGL — they're
    // only called via reflection from Animation Events, so the linker can't
    // see a direct call site otherwise.
    [Preserve]
    [DisallowMultipleComponent]
    public sealed class AnimationShake : MonoBehaviour
    {
        /// <summary>Animation Event entry point. Reads the profile from the event's Object slot.</summary>
        [Preserve]
        public void Play(AnimationEvent ev)
        {
            if (!Application.isPlaying) return;
            if (ev == null) return;
            CameraShake.Impulse(ev.objectReferenceParameter as ShakeProfile);
        }

        /// <summary>Alternate entry point for direct ShakeProfile-only events (no AnimationEvent wrapper).</summary>
        [Preserve]
        public void PlayProfile(ShakeProfile profile)
        {
            if (!Application.isPlaying) return;
            CameraShake.Impulse(profile);
        }
    }
}
