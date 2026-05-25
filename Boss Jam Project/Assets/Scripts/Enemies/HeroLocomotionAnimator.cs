using BossJam.GridSystem;
using BossJam.Player;
using UnityEngine;

namespace BossJam.Enemies
{
    /// <summary>
    /// Drives idle/run animations on the hero based on GridMover state.
    /// Yields to ability-driven animations: while HeroDodge.IsActive or the
    /// hero is stunned, this component stops crossfading so the dash /
    /// melee / stun triggers own the Animator without interference.
    ///
    /// Mirrors the BossLocomotionAnimator pattern.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(GridMover))]
    [RequireComponent(typeof(HeroEnemy))]
    public sealed class HeroLocomotionAnimator : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [Tooltip("State name for the idle clip on the hero animator controller.")]
        [SerializeField] private string idleStateName = "idle";
        [Tooltip("State name for the run/walk clip on the hero animator controller.")]
        [SerializeField] private string runStateName  = "walk";
        [SerializeField, Min(0f)] private float crossfadeSeconds = 0.1f;

        private GridMover mover;
        private HeroEnemy hero;
        private HeroDodge dodge;
        private HeroDashStrike dashStrike;
        private bool? currentRunning;
        private bool idleStateExists;
        private bool runStateExists;

        private void Awake()
        {
            mover = GetComponent<GridMover>();
            hero = GetComponent<HeroEnemy>();
            dodge = GetComponent<HeroDodge>();
            dashStrike = GetComponent<HeroDashStrike>();
            if (animator == null) animator = GetComponentInChildren<Animator>(includeInactive: true);
            idleStateExists = WarnIfStateMissing(idleStateName);
            runStateExists  = WarnIfStateMissing(runStateName);
        }

        private void Update()
        {
            if (animator == null) return;

            // While an ability is mid-clip, we're stunned, OR the hero is dead,
            // yield the Animator so the death clip / ability clip plays without
            // interference. Without the IsDead branch, this Update would crossfade
            // back to idle on the same frame DeathFx.Play() set the death state,
            // clobbering the death anim.
            if ((dodge != null && dodge.IsActive)
                || (dashStrike != null && dashStrike.IsBusy)
                || (hero != null && hero.IsStunned)
                || (hero != null && hero.IsDead))
            {
                currentRunning = null;
                return;
            }

            bool wantRun = mover.IsMoving;
            if (currentRunning != wantRun)
            {
                animator.speed = 1f;
                bool stateOk = wantRun ? runStateExists : idleStateExists;
                if (stateOk)
                    animator.CrossFadeInFixedTime(wantRun ? runStateName : idleStateName, crossfadeSeconds);
                // Update the cached value either way so we don't retry every
                // frame when the state is missing — the Awake warning is the
                // one and only signal the author needs to fix it.
                currentRunning = wantRun;
            }
        }

        // Animator silently no-ops when CrossFadeInFixedTime is given a state
        // name that doesn't exist on the controller — surface it explicitly so
        // a typo or missing state is immediately obvious in the console.
        // Returns true when the state exists (CrossFade is safe to call).
        private bool WarnIfStateMissing(string stateName)
        {
            if (animator == null || string.IsNullOrEmpty(stateName)) return false;
            int hash = Animator.StringToHash(stateName);
            if (animator.HasState(0, hash)) return true;
            Debug.LogWarning($"{nameof(HeroLocomotionAnimator)}: state '{stateName}' not found on " +
                             $"the animator '{animator.name}' (layer 0). Locomotion won't crossfade.", this);
            return false;
        }
    }
}
