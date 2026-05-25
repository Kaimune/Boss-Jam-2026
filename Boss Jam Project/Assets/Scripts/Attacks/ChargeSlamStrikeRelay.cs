using UnityEngine;

namespace BossJam.Attacks
{
    /// <summary>
    /// Animation-event sink for the charge slam. Drop onto the same GameObject
    /// as the boss Animator; the slam clip's AnimationEvent calls
    /// <see cref="Slam"/> at the visual impact frame, and this relay forwards
    /// to the <see cref="ChargeSlamAttack"/> on a sibling node under the boss
    /// root.
    ///
    /// Needed because Unity dispatches AnimationEvents only to components on
    /// the Animator's own GameObject, while the attack lives elsewhere in the
    /// boss hierarchy.
    ///
    /// Set <c>ChargeSlamAttack.autoSlamOnRecovery</c> to false when wiring this
    /// — otherwise the slam fires twice (Recovery-auto + animation-event); the
    /// idempotency latch makes the second a no-op, but the impact moment moves
    /// to whichever fired first. With auto off, the animation event becomes
    /// the sole trigger.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ChargeSlamStrikeRelay : MonoBehaviour
    {
        private ChargeSlamAttack cachedAttack;

        private ChargeSlamAttack Attack
        {
            get
            {
                if (cachedAttack != null) return cachedAttack;
                cachedAttack = transform.root.GetComponentInChildren<ChargeSlamAttack>(includeInactive: true);
                return cachedAttack;
            }
        }

        public void Slam()
        {
            var a = Attack;
            if (a != null) a.Slam();
        }
    }
}
