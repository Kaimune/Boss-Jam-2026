using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BossJam.Attacks
{
    /// <summary>
    /// Watches an IAttack on the same GameObject and plays a single Animator state
    /// when the attack successfully initiates (Idle/Cooldown → Windup), then returns
    /// to idle when the attack finishes. The state name is read from
    /// <see cref="AttackConfig.attackStateName"/>; leave it blank to skip animation.
    ///
    /// Also guarantees the clip's AnimationEvents (Strike, SFX, screen-shake) still
    /// fire even when a follow-up attack crossfades the Animator away mid-clip. On
    /// each attack start, a coroutine watches the cached event timeline: any event
    /// the visible Animator advances past naturally is left alone, but anything the
    /// Animator never reached (because the visible state changed) gets dispatched
    /// from here via SendMessage on the Animator's GameObject — same receivers
    /// Unity would have hit. The damage moment stays anchored to the authored
    /// keyframe time even though the visible animation was cut short.
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
        private readonly Dictionary<string, AnimationEvent[]> clipEventsByName = new Dictionary<string, AnimationEvent[]>();
        private Coroutine eventDispatchRoutine;

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
            clipEventsByName.Clear();
            if (animator?.runtimeAnimatorController == null) return;
            foreach (var clip in animator.runtimeAnimatorController.animationClips)
            {
                if (clip == null || clipLengthByName.ContainsKey(clip.name)) continue;
                clipLengthByName[clip.name] = clip.length;
                // clip.events is already sorted by time; copy so external mutation
                // of the array doesn't desync our scheduler.
                var src = clip.events;
                if (src != null && src.Length > 0)
                {
                    var copy = new AnimationEvent[src.Length];
                    System.Array.Copy(src, copy, src.Length);
                    clipEventsByName[clip.name] = copy;
                }
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

                float playbackSpeed = autoFitAnimationSpeed ? ComputeFitSpeed(stateName) : 1f;
                animator.speed = playbackSpeed;
                animator.CrossFadeInFixedTime(stateName, crossfadeSeconds);
                Debug.Log($"[AttackAnimBinder:{name}] CrossFade '{stateName}' speed={animator.speed:F3} (clipLen={(clipLengthByName.TryGetValue(stateName,out var cl)?cl:-1):F2})", this);

                if (eventDispatchRoutine != null) StopCoroutine(eventDispatchRoutine);
                eventDispatchRoutine = StartCoroutine(EnsureClipEventsFire(stateName, playbackSpeed));
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

        // Walks the cached AnimationEvent timeline in lockstep with the visible
        // Animator. For each event we latch "fired by the Animator" the moment we
        // observe the clip's sample time advance past it while still in the attack
        // state; once the scheduled wall-clock moment arrives, any unlatched event
        // gets dispatched here so a mid-clip interrupt doesn't eat the strike /
        // SFX / shake keyframes.
        private IEnumerator EnsureClipEventsFire(string stateName, float playbackSpeed)
        {
            if (!clipEventsByName.TryGetValue(stateName, out var events) || events.Length == 0)
            {
                eventDispatchRoutine = null;
                yield break;
            }
            if (playbackSpeed <= 0.0001f) playbackSpeed = 1f;
            int stateHash = Animator.StringToHash(stateName);
            var firedByAnimator = new bool[events.Length];
            int nextScheduled = 0;
            float startTime = Time.time;

            while (nextScheduled < events.Length && animator != null)
            {
                // Latch any event whose authored frame the Animator has visibly
                // reached. shortNameHash matches even when CrossFade is mid-blend
                // (state is "current" the instant the transition begins).
                var info = animator.GetCurrentAnimatorStateInfo(0);
                if (info.shortNameHash == stateHash && info.length > 0f)
                {
                    float wrapped = info.normalizedTime - Mathf.Floor(info.normalizedTime);
                    float clipSeconds = wrapped * info.length;
                    for (int i = nextScheduled; i < events.Length; i++)
                        if (clipSeconds >= events[i].time) firedByAnimator[i] = true;
                }

                float elapsed = Time.time - startTime;
                while (nextScheduled < events.Length && elapsed >= events[nextScheduled].time / playbackSpeed)
                {
                    if (!firedByAnimator[nextScheduled])
                        Dispatch(animator.gameObject, events[nextScheduled]);
                    nextScheduled++;
                }
                if (nextScheduled >= events.Length) break;
                yield return null;
            }
            eventDispatchRoutine = null;
        }

        // Two receiver shapes are used in this project:
        //   Play(AnimationEvent ev)       — AnimationShake, AnimationSfx
        //   Strike() / Slam() / no args   — strike relays
        // SendMessage dispatches by argument type, and no receiver overloads both
        // shapes, so calling each form once hits at most one method per receiver.
        private static void Dispatch(GameObject target, AnimationEvent ev)
        {
            if (target == null || ev == null) return;
            string fn = ev.functionName;
            if (string.IsNullOrEmpty(fn)) return;
            target.SendMessage(fn, ev, SendMessageOptions.DontRequireReceiver);
            target.SendMessage(fn, SendMessageOptions.DontRequireReceiver);
        }
    }
}
