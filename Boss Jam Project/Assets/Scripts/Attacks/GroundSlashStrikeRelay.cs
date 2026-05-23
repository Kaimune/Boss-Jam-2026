using UnityEngine;

namespace BossJam.Attacks
{
    /// <summary>
    /// Animation-event sink for the ground slash. Drop onto the same GameObject
    /// as the boss Animator; the slash clip's AnimationEvent calls
    /// <see cref="Strike"/> at the visual contact frame, and this relay forwards
    /// to the <see cref="GroundSlashAttack"/> on a sibling node under the boss
    /// root.
    ///
    /// Needed because Unity dispatches AnimationEvents only to components on
    /// the Animator's own GameObject, while the attack lives elsewhere in the
    /// boss hierarchy (typically Boss/BossAttack_GroundSlash, sibling to the
    /// visual prefab that owns the Animator).
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

        private GroundSlashAttack Attack
        {
            get
            {
                if (cachedAttack != null) return cachedAttack;
                // Climb to the boss root then descend — covers the common case
                // where this relay sits on the visual prefab and the attack
                // lives on a sibling GameObject.
                cachedAttack = transform.root.GetComponentInChildren<GroundSlashAttack>(includeInactive: true);
                return cachedAttack;
            }
        }

        public void Strike()
        {
            var a = Attack;
            if (a != null) a.Strike();
        }

        // Alias kept for clips authored against an earlier prototype receiver
        // named PlayForwardSlash. New clips should call Strike directly.
        public void PlayForwardSlash() => Strike();
    }
}
