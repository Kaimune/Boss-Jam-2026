using System.Collections.Generic;
using BossJam.Attacks;
using UnityEngine;

namespace BossJam.Audio
{
    /// <summary>
    /// Death feedback bundle: plays a death SFX through <see cref="AudioDirector"/>
    /// and snaps the Animator into a "death" state when <see cref="Play"/>
    /// is called. Drop one on the Boss prefab and one on the Hero prefab; the
    /// owning script calls Play() from its lethal-damage branch.
    ///
    /// Also exposes <see cref="ClipLengthSeconds"/> so the OutroDirector can size
    /// its post-death wait to the clip itself (instead of the hard-coded
    /// heroDeathHoldSeconds), keeping the dialogue cue aligned with the visual.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DeathFx : MonoBehaviour
    {
        [SerializeField] private AudioClip deathSfx;
        [SerializeField] private Animator animator;
        [SerializeField] private string deathStateName = "hero_die_stepped";
        [SerializeField, Tooltip("When true, Animator.Play() is used for an instant cut — anything currently " +
                                 "blending in or playing (attack mid-recovery, locomotion, hit reaction) is " +
                                 "discarded. When false, falls back to a soft crossfade.")]
        private bool hardOverride = true;
        [SerializeField, Min(0f), Tooltip("Crossfade duration used only when hardOverride is false.")]
        private float crossfadeSeconds = 0.05f;

        private readonly Dictionary<string, float> clipLengthByName = new Dictionary<string, float>();

        private void Awake()
        {
            if (animator == null)
                animator = transform.root.GetComponentInChildren<Animator>(includeInactive: true);
            CacheClipLengths();
        }

        public float ClipLengthSeconds
        {
            get
            {
                if (string.IsNullOrEmpty(deathStateName)) return 0f;
                return clipLengthByName.TryGetValue(deathStateName, out var len) ? len : 0f;
            }
        }

        public void Play()
        {
            AudioDirector.Sfx(deathSfx);
            if (animator == null || string.IsNullOrEmpty(deathStateName)) return;

            // Silence anything that could re-drive the animator after we've snapped
            // to death (AttackAnimationBinder's Recovery→Cooldown fires an idle
            // CrossFade even after the entity dies mid-attack). Locomotion animators
            // already self-guard via IsDead.
            DisableCompetingAnimators();

            // Reset speed in case AttackAnimationBinder previously scaled it to
            // fit an attack clip; the death clip plays at its authored rate.
            animator.speed = 1f;
            if (hardOverride)
                animator.Play(deathStateName, 0, 0f);
            else
                animator.CrossFadeInFixedTime(deathStateName, crossfadeSeconds);
        }

        private void DisableCompetingAnimators()
        {
            var root = transform.root;
            var binders = root.GetComponentsInChildren<AttackAnimationBinder>(includeInactive: true);
            for (int i = 0; i < binders.Length; i++)
                if (binders[i] != null) binders[i].enabled = false;
        }

        private void CacheClipLengths()
        {
            clipLengthByName.Clear();
            if (animator == null || animator.runtimeAnimatorController == null) return;
            foreach (var clip in animator.runtimeAnimatorController.animationClips)
            {
                if (clip != null && !clipLengthByName.ContainsKey(clip.name))
                    clipLengthByName[clip.name] = clip.length;
            }
        }
    }
}
