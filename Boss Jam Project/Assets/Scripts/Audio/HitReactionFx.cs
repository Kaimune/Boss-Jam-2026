using BossJam.Attacks;
using UnityEngine;

namespace BossJam.Audio
{
    /// <summary>
    /// Damage feedback bundle: plays a hurt SFX through <see cref="AudioDirector"/>
    /// and crossfades the Animator into a "take damage" state when <see cref="Play"/>
    /// is called. Drop one on the Boss prefab and one on the Hero prefab; the
    /// owning script calls Play() from TakeDamage when HP actually drops.
    ///
    /// While any IAttack on this object (or its children) is mid-swing
    /// (Windup/Active/Recovery), the animator crossfade is suppressed — the hurt
    /// anim must not interrupt an attack the player has already committed to. The
    /// SFX still plays so the hit is audible.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HitReactionFx : MonoBehaviour
    {
        [SerializeField] private AudioClip hurtSfx;
        [SerializeField] private Animator animator;
        [SerializeField] private string hurtStateName = "take dmg_stepped";
        [SerializeField, Min(0f)] private float crossfadeSeconds = 0.05f;

        private IAttack[] attacks;

        private void Awake()
        {
            if (animator == null)
                animator = transform.root.GetComponentInChildren<Animator>(includeInactive: true);
            attacks = GetComponentsInChildren<IAttack>(includeInactive: true);
        }

        public void Play()
        {
            AudioDirector.Sfx(hurtSfx);
            if (animator == null || string.IsNullOrEmpty(hurtStateName)) return;
            if (IsAnyAttackBusy()) return;
            animator.speed = 1f;
            animator.CrossFadeInFixedTime(hurtStateName, crossfadeSeconds);
        }

        private bool IsAnyAttackBusy()
        {
            if (attacks == null) return false;
            for (int i = 0; i < attacks.Length; i++)
            {
                var a = attacks[i];
                if (a == null) continue;
                var s = a.State;
                if (s != AttackState.Idle && s != AttackState.Cooldown) return true;
            }
            return false;
        }
    }
}
