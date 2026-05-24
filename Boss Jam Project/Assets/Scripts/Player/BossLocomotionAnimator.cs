using System.Collections.Generic;
using BossJam.Attacks;
using UnityEngine;

namespace BossJam.Player
{
    /// <summary>
    /// Drives idle/run animations on the boss based on GridMover state. Yields to
    /// attack-driven animations: while any IAttack is busy, this component does
    /// nothing (AttackAnimationBinder owns the animator during attacks).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(GridMover))]
    public class BossLocomotionAnimator : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private string idleStateName = "idle_stepped";
        [SerializeField] private string runStateName  = "run_stepped";
        [SerializeField, Min(0f)] private float crossfadeSeconds = 0.1f;

        private GridMover mover;
        private readonly List<IAttack> attacks = new List<IAttack>();
        private bool? currentRunning;   // nullable forces a fresh transition after yielding

        private void Awake()
        {
            mover = GetComponent<GridMover>();
            if (animator == null) animator = transform.root.GetComponentInChildren<Animator>(includeInactive: true);
            GetComponentsInChildren<IAttack>(true, attacks);
        }

        private void Update()
        {
            if (animator == null) return;

            // Defer to AttackAnimationBinder only while an attack's *animation* is
            // playing (Windup/Active/Recovery). Cooldown is a post-animation logical
            // wait — the attack clip is already done, and movement isn't locked, so
            // locomotion needs to resume immediately or the boss visibly freezes in
            // idle for the whole cooldown (up to 5s on Ult) while the player moves.
            for (int i = 0; i < attacks.Count; i++)
            {
                var a = attacks[i];
                if (a == null) continue;
                var s = a.State;
                if (s != AttackState.Idle && s != AttackState.Cooldown)
                {
                    currentRunning = null;   // re-evaluate next time we own the animator
                    return;
                }
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
