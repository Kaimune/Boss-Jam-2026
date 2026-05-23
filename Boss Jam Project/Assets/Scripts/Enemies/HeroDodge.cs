using BossJam.Difficulty;
using UnityEngine;

namespace BossJam.Enemies
{
    /// <summary>
    /// Hero's defensive ability — speed boost in the current kite direction
    /// for dodgeDurationSeconds. No iframes: the hero relies on being faster
    /// to clear hazards, not on invulnerability.
    ///
    /// Spec 1: wired but never picked — Score returns 0. Spec 3 (hazard
    /// awareness) raises the score when an unavoidable hazard is closing in.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(HeroEnemy))]
    public sealed class HeroDodge : MonoBehaviour, IHeroAbility
    {
        [Header("Animation")]
        [Tooltip("Animator that owns the hero's clips. Auto-resolves via GetComponentInChildren if left null.")]
        [SerializeField] private Animator animator;
        [Tooltip("Trigger fired on the Animator when the dash begins. Match the parameter name on your controller.")]
        [SerializeField] private string animatorTrigger = "Dash";

        public string Id => "dodge";
        // Busy while the boost is active so the brain doesn't immediately
        // pick another ability that would conflict with the speed override.
        public bool IsBusy => Time.time < activeUntil;
        public bool LocksMovement => false;     // dodge ENHANCES movement; never locks it

        private float cooldownReadyAt;
        private float activeUntil;
        private float lockedSpeedMul;           // snapshot of dodge mul at Begin time
        private Vector2 lockedDirection;
        private HeroEnemy hero;
        private DifficultyRuntime rt;

        // HeroEnemy queries this each frame to know whether to apply the
        // boost and which direction to lock the kite vector to.
        public bool IsActive => Time.time < activeUntil;
        public float SpeedMultiplier => lockedSpeedMul;
        public Vector2 LockedDirection => lockedDirection;

        private void Awake()
        {
            hero = GetComponent<HeroEnemy>();
            rt = FindFirstObjectByType<DifficultyRuntime>();
            if (animator == null) animator = GetComponentInChildren<Animator>(includeInactive: true);
        }

        public bool IsReady => !IsActive && Time.time >= cooldownReadyAt;

        public float Score(in HeroDecisionContext ctx) => 0f;

        public void Begin(in HeroDecisionContext ctx)
        {
            if (animator != null && !string.IsNullOrEmpty(animatorTrigger))
                animator.SetTrigger(animatorTrigger);

            // Lock the boost direction at commit time so a mid-dodge kite
            // re-evaluation doesn't tug the hero around. The boost has a
            // committed feel — once you press, you go that way.
            lockedDirection = ctx.kiteDir.sqrMagnitude > 0.0001f
                ? ctx.kiteDir.normalized
                : Vector2.zero;
            lockedSpeedMul = Eff(Target.HeroDodgeSpeedMultiplier, hero.Config.dodgeSpeedMultiplier);

            float dur = Eff(Target.HeroDodgeDurationSeconds, hero.Config.dodgeDurationSeconds);
            activeUntil = Time.time + dur;
            cooldownReadyAt = activeUntil + Eff(Target.HeroDodgeCooldownSeconds, hero.Config.dodgeCooldownSeconds);
        }

        public void Tick(float dt) { }

        public void Cancel()
        {
            activeUntil = 0f;
            cooldownReadyAt = Time.time;
        }

        private float Eff(Target t, float b) => rt != null ? rt.Get(t, b) : b;
    }
}
