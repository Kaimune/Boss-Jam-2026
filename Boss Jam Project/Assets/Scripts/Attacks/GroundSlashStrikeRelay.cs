using UnityEngine;

namespace BossJam.Attacks
{
    /// <summary>
    /// Animation-event sink for the ground slash. Drop onto the same GameObject
    /// as the boss Animator; the slash clip's AnimationEvent calls
    /// <see cref="Strike"/> at the visual contact frame, and this relay forwards
    /// to the parent <see cref="GroundSlashAttack"/>.
    ///
    /// Needed because Unity dispatches AnimationEvents only to components on the
    /// Animator's own GameObject, while the attack lives on a parent.
    ///
    /// Set <c>GroundSlashAttack.autoStrikeOnActive</c> to false when wiring this
    /// — otherwise Strike fires twice (FSM-auto + animation-event) and the
    /// idempotency latch makes the second a no-op, but the damage moment moves
    /// to whichever came first (typically the auto-fire). With auto off, the
    /// animation event becomes the sole trigger.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GroundSlashStrikeRelay : MonoBehaviour
    {
        private GroundSlashAttack cachedAttack;

        private GroundSlashAttack Attack =>
            cachedAttack != null
                ? cachedAttack
                : (cachedAttack = GetComponentInParent<GroundSlashAttack>(includeInactive: true));

        public void Strike()
        {
            var a = Attack;
            if (a != null) a.Strike();
        }
    }
}
