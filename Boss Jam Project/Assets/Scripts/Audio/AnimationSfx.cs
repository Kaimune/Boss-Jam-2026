using UnityEngine;
using UnityEngine.Scripting;

namespace BossJam.Audio
{
    /// <summary>
    /// Bridge between Animation Events and <see cref="AudioDirector"/>. Attach to
    /// the same GameObject as the Animator. Author a keyframe in the Animation
    /// window, set Function = <c>PlaySfx</c>, and drop an AudioClip into the
    /// event's Object slot — the clip plays through the AudioDirector SFX bus
    /// at that frame.
    /// </summary>
    // [Preserve] blocks IL2CPP from stripping these methods on WebGL — they're
    // only called via reflection from Animation Events, so the linker can't
    // see a direct call site otherwise.
    [Preserve]
    [DisallowMultipleComponent]
    public sealed class AnimationSfx : MonoBehaviour
    {
        /// <summary>Animation Event entry point. Reads the clip from the event's Object slot.</summary>
        [Preserve]
        public void PlaySfx(AnimationEvent ev)
        {
            if (ev == null) return;
            AudioDirector.Sfx(ev.objectReferenceParameter as AudioClip);
        }

        /// <summary>Alternate entry point for direct AudioClip-only events (no AnimationEvent wrapper).</summary>
        [Preserve]
        public void PlayClip(AudioClip clip) => AudioDirector.Sfx(clip);
    }
}
