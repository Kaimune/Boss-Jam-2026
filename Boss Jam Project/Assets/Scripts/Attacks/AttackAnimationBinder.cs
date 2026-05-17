using System.Collections.Generic;
using UnityEngine;

namespace BossJam.Attacks
{
    /// <summary>
    /// Watches an IAttack on the same GameObject and plays Animator states on phase
    /// transitions. Reads state-name strings from AttackConfig (windupTrigger /
    /// activeTrigger / recoveryTrigger — the strings are interpreted as state names,
    /// not Animator trigger parameters, so no controller surgery is needed).
    ///
    /// Auto-fits playback: each phase's animation is sped/slowed so it completes in
    /// exactly the phase duration. animator.speed is reset to 1 when returning to idle.
    ///
    /// Composition: no inheritance, just a phase-event subscriber. Drop next to any
    /// IAttack to give it animations; leave the Config trigger fields blank to skip.
    /// </summary>
    [DisallowMultipleComponent]
    public class AttackAnimationBinder : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField, Min(0f)] private float crossfadeSeconds = 0.05f;
        [Tooltip("State to fade back to on Cooldown/Idle. Leave blank to do nothing.")]
        [SerializeField] private string idleStateName = "idle_stepped";
        [Tooltip("If false, animator.speed stays 1 and animations may get truncated when phases end.")]
        [SerializeField] private bool autoFitAnimationSpeed = true;

        private IAttack attack;
        private readonly Dictionary<string, float> clipLengthByName = new Dictionary<string, float>();

        private void Awake()
        {
            if (animator == null)
            {
                var root = transform.root;
                animator = root.GetComponentInChildren<Animator>(includeInactive: true);
            }
            attack = GetComponent<IAttack>();
            if (attack == null)
            {
                Debug.LogWarning($"{nameof(AttackAnimationBinder)} on {name}: no IAttack component found on this GameObject.", this);
                enabled = false;
                return;
            }
            CacheClipLengths();
            attack.StateChanged += OnPhase;
        }

        private void OnDestroy()
        {
            if (attack != null) attack.StateChanged -= OnPhase;
        }

        private void CacheClipLengths()
        {
            clipLengthByName.Clear();
            if (animator?.runtimeAnimatorController == null) return;
            foreach (var clip in animator.runtimeAnimatorController.animationClips)
            {
                if (clip != null && !clipLengthByName.ContainsKey(clip.name))
                    clipLengthByName[clip.name] = clip.length;
            }
        }

        private void OnPhase(AttackState prev, AttackState next)
        {
            if (animator == null || attack.Config == null) return;
            var stateName = StateNameFor(next);
            if (string.IsNullOrEmpty(stateName)) return;

            animator.speed = autoFitAnimationSpeed
                ? ComputeFitSpeed(stateName, next)
                : 1f;
            animator.CrossFadeInFixedTime(stateName, crossfadeSeconds);
        }

        private float ComputeFitSpeed(string stateName, AttackState phase)
        {
            // Idle/Cooldown play at natural speed — no auto-fit.
            if (phase == AttackState.Idle || phase == AttackState.Cooldown) return 1f;
            if (!clipLengthByName.TryGetValue(stateName, out var clipLength) || clipLength <= 0f) return 1f;

            var phaseDuration = PhaseDurationSeconds(phase);
            if (phaseDuration <= 0f) return 1f;
            return clipLength / phaseDuration;
        }

        private float PhaseDurationSeconds(AttackState phase)
        {
            var c = attack.Config;
            switch (phase)
            {
                case AttackState.Windup:   return c.windupSeconds;
                case AttackState.Active:   return c.activeSeconds;
                case AttackState.Recovery: return c.recoverySeconds;
                case AttackState.Cooldown: return c.cooldownSeconds;
                default: return 0f;
            }
        }

        private string StateNameFor(AttackState s)
        {
            var c = attack.Config;
            switch (s)
            {
                case AttackState.Windup:   return c.windupTrigger;
                case AttackState.Active:   return c.activeTrigger;
                case AttackState.Recovery: return c.recoveryTrigger;
                case AttackState.Cooldown: return idleStateName;
                case AttackState.Idle:     return idleStateName;
                default: return null;
            }
        }
    }
}
