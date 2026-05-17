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

            // While any attack is busy, defer to AttackAnimationBinder.
            for (int i = 0; i < attacks.Count; i++)
            {
                if (attacks[i] != null && attacks[i].IsBusy)
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
