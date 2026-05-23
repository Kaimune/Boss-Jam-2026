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
        [Tooltip("Whether the hero has this ability by default. A DifficultyRuntime modifier on " +
                 "Target.HeroMeleeEnabled can override this — Override op with value 0 disables, 1 enables.")]
        [SerializeField] private bool enabledByDefault = true;

        public bool IsEnabled =>
            (rt != null ? rt.Get(Target.HeroMeleeEnabled, enabledByDefault ? 1f : 0f) : (enabledByDefault ? 1f : 0f)) > 0.5f;

        [Header("Animation")]
        [Tooltip("Animator that owns the hero's clips. Auto-resolves via GetComponentInChildren if left null.")]
        [SerializeField] private Animator animator;
        [Tooltip("State name to crossfade into when the swing begins. Match the state node name on your controller.")]
        [SerializeField] private string animatorStateName = "attack";
        [SerializeField, Min(0f)] private float crossfadeSeconds = 0.05f;
        [Tooltip("Fallback busy duration in seconds. Only used when the clip length can't be resolved from the " +
                 "animator controller — e.g. when no clip matches the state name. Normally the actual clip length is used.")]
        [SerializeField, Min(0f)] private float fallbackBusySeconds = 0.5f;

        private float busyUntil;
        private float resolvedClipSeconds;

        public string Id => "melee";
        public bool IsBusy => Time.time < busyUntil;
        public bool LocksMovement => IsBusy;

        private float cooldownReadyAt;
        private HeroEnemy hero;
        private DifficultyRuntime rt;
        private bool stateExists;

        private void Awake()
        {
            hero = GetComponent<HeroEnemy>();
            rt = FindFirstObjectByType<DifficultyRuntime>();
            if (animator == null) animator = GetComponentInChildren<Animator>(includeInactive: true);
            if (animator != null && !string.IsNullOrEmpty(animatorStateName))
            {
                stateExists = animator.HasState(0, Animator.StringToHash(animatorStateName));
                if (!stateExists)
                    Debug.LogWarning($"{nameof(HeroMelee)}: state '{animatorStateName}' not found on " +
                                     $"animator '{animator.name}' (layer 0). Swing animation will not play.", this);

                // Cache the clip length so the busy/movement-lock window
                // matches the actual animation. Assumes the state's clip
                // shares its name — Unity's default when you drop a clip
                // into a controller. If not, falls back to fallbackBusySeconds.
                if (animator.runtimeAnimatorController != null)
                {
                    var clips = animator.runtimeAnimatorController.animationClips;
                    for (int i = 0; i < clips.Length; i++)
                    {
                        if (clips[i] != null && clips[i].name == animatorStateName)
                        {
                            resolvedClipSeconds = clips[i].length;
                            break;
                        }
                    }
                }
                if (resolvedClipSeconds <= 0f)
                    resolvedClipSeconds = fallbackBusySeconds;
            }
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
            if (animator != null && stateExists)
                animator.CrossFadeInFixedTime(animatorStateName, crossfadeSeconds);
            busyUntil = Time.time + resolvedClipSeconds;

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
            busyUntil = Time.time;
        }

        private float Eff(Target t, float b) => rt != null ? rt.Get(t, b) : b;
        private int   EffI(Target t, int b)  => rt != null ? rt.GetInt(t, b) : b;
    }
}
