using BossJam.Difficulty;
using BossJam.Player;
using UnityEngine;

namespace BossJam.Enemies
{
    /// <summary>
    /// Hero's close-range ability — instant directional hit toward the boss.
    /// No telegraph, no hitbox prefab in Spec 1: the swing is committed in
    /// Begin(), boss damage applied immediately if it's inside meleeRangeCells.
    ///
    /// Scoring: high (0.9) when in range AND the boss is in a punish window
    /// (Recovery/Cooldown). Otherwise zero — hero only commits when the boss
    /// can't immediately respond. The "one hit per window" rule is enforced
    /// by HeroBrain, not here.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(HeroEnemy))]
    public sealed class HeroMelee : MonoBehaviour, IHeroAbility
    {
        [Header("Animation")]
        [Tooltip("Animator that owns the hero's clips. Auto-resolves via GetComponentInChildren if left null.")]
        [SerializeField] private Animator animator;
        [Tooltip("Trigger fired on the Animator when the swing begins. Match the parameter name on your controller.")]
        [SerializeField] private string animatorTrigger = "Attack";

        public string Id => "melee";
        public bool IsBusy => false;            // instant swing — no busy window
        public bool LocksMovement => false;

        private float cooldownReadyAt;
        private HeroEnemy hero;
        private DifficultyRuntime rt;

        private void Awake()
        {
            hero = GetComponent<HeroEnemy>();
            rt = FindFirstObjectByType<DifficultyRuntime>();
            if (animator == null) animator = GetComponentInChildren<Animator>(includeInactive: true);
        }

        public bool IsReady => Time.time >= cooldownReadyAt;

        public float Score(in HeroDecisionContext ctx)
        {
            if (!ctx.bossInPunishWindow) return 0f;
            float range = Eff(Target.HeroMeleeRangeCells, hero.Config.meleeRangeCells);
            if (ctx.distanceToBossCells > range) return 0f;
            return 0.9f;
        }

        public void Begin(in HeroDecisionContext ctx)
        {
            if (animator != null && !string.IsNullOrEmpty(animatorTrigger))
                animator.SetTrigger(animatorTrigger);

            float range = Eff(Target.HeroMeleeRangeCells, hero.Config.meleeRangeCells);
            // Re-check range at swing time — distance could have edged out
            // by a fraction of a cell between Score and Begin.
            if (ctx.distanceToBossCells <= range)
            {
                var boss = FindFirstObjectByType<BossController>();
                if (boss != null)
                {
                    int damage = EffI(Target.HeroMeleeDamage, hero.Config.meleeDamage);
                    boss.TakeDamage(damage, hero);
                }
            }
            float cooldown = Eff(Target.HeroMeleeCooldownSeconds, hero.Config.meleeCooldownSeconds);
            cooldownReadyAt = Time.time + cooldown;
        }

        public void Tick(float dt) { }

        public void Cancel()
        {
            cooldownReadyAt = Time.time;
        }

        private float Eff(Target t, float b) => rt != null ? rt.Get(t, b) : b;
        private int   EffI(Target t, int b)  => rt != null ? rt.GetInt(t, b) : b;
    }
}
