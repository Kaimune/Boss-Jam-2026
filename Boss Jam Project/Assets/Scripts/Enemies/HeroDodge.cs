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
        [Tooltip("Whether the hero has this ability by default. A DifficultyRuntime modifier on " +
                 "Target.HeroDodgeEnabled can override this — Override op with value 0 disables, 1 enables.")]
        [SerializeField] private bool enabledByDefault = false;

        public bool IsEnabled =>
            (rt != null ? rt.Get(Target.HeroDodgeEnabled, enabledByDefault ? 1f : 0f) : (enabledByDefault ? 1f : 0f)) > 0.5f;

        [Header("Animation")]
        [Tooltip("Animator that owns the hero's clips. Auto-resolves via GetComponentInChildren if left null.")]
        [SerializeField] private Animator animator;
        [Tooltip("State name to crossfade into when the dash begins. Match the state node name on your controller.")]
        [SerializeField] private string animatorStateName = "dash";
        [SerializeField, Min(0f)] private float crossfadeSeconds = 0.05f;

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
        private bool stateExists;

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
            if (animator != null && !string.IsNullOrEmpty(animatorStateName))
            {
                stateExists = animator.HasState(0, Animator.StringToHash(animatorStateName));
                if (!stateExists)
                    Debug.LogWarning($"{nameof(HeroDodge)}: state '{animatorStateName}' not found on " +
                                     $"animator '{animator.name}' (layer 0). Dash animation will not play.", this);
            }
        }

        public bool IsReady => !IsActive && Time.time >= cooldownReadyAt;

        public float Score(in HeroDecisionContext ctx)
        {
            // Primary signal: hero is currently inside a perceived hazard rect.
            // That's the unavoidable "I'm getting hit unless I move" moment —
            // dash out at high priority.
            var hazards = hero != null ? hero.PerceivedHazards : null;
            if (hazards != null)
            {
                Vector2 hero01 = ctx.heroCenter;
                for (int i = 0; i < hazards.Count; i++)
                {
                    var h = hazards[i];
                    if (PointInRect(hero01, h.Anchor, h.Footprint)) return 0.95f;
                }
            }

            // Fallback signal: boss is winding up or actively swinging. Even
            // if no specific hazard rect is published yet (e.g. boss in early
            // windup before the telegraph spawns), dodge defensively.
            return ctx.bossIsExecutingAttack ? 0.6f : 0f;
        }

        private static bool PointInRect(Vector2 p, Vector2 anchor, Vector2 footprint)
        {
            return p.x >= anchor.x && p.x <= anchor.x + footprint.x
                && p.y >= anchor.y && p.y <= anchor.y + footprint.y;
        }

        public void Begin(in HeroDecisionContext ctx)
        {
            if (animator != null && stateExists)
                animator.CrossFadeInFixedTime(animatorStateName, crossfadeSeconds);

            // Lock the boost direction at commit time so a mid-dodge kite
            // re-evaluation doesn't tug the hero around. The boost has a
            // committed feel — once you press, you go that way.
            lockedDirection = ctx.kiteDir.sqrMagnitude > 0.0001f
                ? ctx.kiteDir.normalized
                : Vector2.zero;
            lockedSpeedMul = Eff(Target.HeroDodgeSpeedMultiplier, hero.Config.dodgeSpeedMultiplier);

            float dur = Eff(Target.HeroDodgeDurationSeconds, hero.Config.dodgeDurationSeconds);
            activeUntil = Time.time + dur;
            // Cooldown starts on cast (intuitive: the cast itself is what
            // triggers the timer). The dodge still can't re-fire while
            // IsActive — IsReady already gates on that. Effective gate is
            // max(dur, cooldown) from cast time.
            cooldownReadyAt = Time.time + Eff(Target.HeroDodgeCooldownSeconds, hero.Config.dodgeCooldownSeconds);
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
