using System.Collections.Generic;
using UnityEngine;

namespace BossJam.Attacks
{
    /// <summary>
    /// Watches an IAttack on the same GameObject and plays a single Animator state
    /// when the attack successfully initiates (Idle/Cooldown → Windup), then returns
    /// to idle when the attack finishes. The state name is read from
    /// <see cref="AttackConfig.attackStateName"/>; leave it blank to skip animation.
    /// </summary>
    [DisallowMultipleComponent]
    public class AttackAnimationBinder : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField, Min(0f)] private float crossfadeSeconds = 0.05f;
        [Tooltip("State to fade back to when the attack returns to Cooldown/Idle. Blank = do nothing.")]
        [SerializeField] private string idleStateName = "idle_stepped";
        [Tooltip("If true, animator.speed is set so the clip finishes in exactly Windup+Active+Recovery.")]
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
            Debug.Log($"[AttackAnimBinder:{name}] Awake — attack={attack?.GetType().Name} cfg={attack?.Config?.id} stateName='{attack?.Config?.attackStateName}' animator={(animator!=null?animator.name+" ctrl="+(animator.runtimeAnimatorController!=null?animator.runtimeAnimatorController.name:"NULL"):"NULL")} clipCount={clipLengthByName.Count} has_jump_stepped={clipLengthByName.ContainsKey("jump_stepped")}", this);
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
            Debug.Log($"[AttackAnimBinder:{name}] OnPhase {prev}->{next} (cfg={attack?.Config?.id} stateName='{attack?.Config?.attackStateName}' animator={(animator!=null?animator.name:"NULL")})", this);
            if (animator == null || attack.Config == null) return;

            // Initiate: any transition INTO Windup is a "boss successfully started an attack".
            if (next == AttackState.Windup)
            {
                var stateName = attack.Config.attackStateName;
                if (string.IsNullOrEmpty(stateName)) return;

                animator.speed = autoFitAnimationSpeed ? ComputeFitSpeed(stateName) : 1f;
                animator.CrossFadeInFixedTime(stateName, crossfadeSeconds);
                Debug.Log($"[AttackAnimBinder:{name}] CrossFade '{stateName}' speed={animator.speed:F3} (clipLen={(clipLengthByName.TryGetValue(stateName,out var cl)?cl:-1):F2})", this);
                return;
            }

            // Attack clip is done at Recovery→Cooldown — fade out to idle here.
            // Don't fade again on Cooldown→Idle: the visual is already handed off
            // to BossLocomotionAnimator (which may be showing run_stepped if the
            // player is moving), and another idle CrossFade here would clobber it.
            if (next == AttackState.Cooldown)
            {
                animator.speed = 1f;
                if (!string.IsNullOrEmpty(idleStateName))
                    animator.CrossFadeInFixedTime(idleStateName, crossfadeSeconds);
            }
        }

        private float ComputeFitSpeed(string stateName)
        {
            if (!clipLengthByName.TryGetValue(stateName, out var clipLength) || clipLength <= 0f) return 1f;
            var c = attack.Config;
            var total = c.windupSeconds + c.activeSeconds + c.recoverySeconds;
            return total > 0f ? clipLength / total : 1f;
        }
    }
}
