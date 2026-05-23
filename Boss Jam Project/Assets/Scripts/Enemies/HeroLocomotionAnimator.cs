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
        private bool? currentRunning;

        private void Awake()
        {
            mover = GetComponent<GridMover>();
            hero = GetComponent<HeroEnemy>();
            dodge = GetComponent<HeroDodge>();
            if (animator == null) animator = GetComponentInChildren<Animator>(includeInactive: true);
        }

        private void Update()
        {
            if (animator == null) return;

            // While a dash is active or we're stunned, yield the Animator to
            // the trigger-driven clips so they aren't interrupted.
            if ((dodge != null && dodge.IsActive) || (hero != null && hero.IsStunned))
            {
                currentRunning = null;
                return;
            }

            bool wantRun = mover.IsMoving;
            if (currentRunning != wantRun)
            {
                animator.speed = 1f;
                animator.CrossFadeInFixedTime(wantRun ? runStateName : idleStateName, crossfadeSeconds);
                currentRunning = wantRun;
            }
        }
    }
}
