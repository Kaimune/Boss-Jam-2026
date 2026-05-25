using BossJam.Difficulty;
using BossJam.GridSystem;
using BossJam.Player;
using UnityEngine;

namespace BossJam.Enemies
{
    /// <summary>
    /// Hero's commit attack — a short dash toward the boss that resolves a
    /// single hit when the hero enters melee range (or when the dash duration
    /// elapses, whichever comes first). Replaces the old planted-swing melee.
    ///
    /// The dash itself drives movement: while IsActive, HeroEnemy uses
    /// CurrentDirection as the input vector and applies SpeedMultiplier to the
    /// mover tick. Hero is fully vulnerable during the dash — there are no
    /// i-frames here.
    ///
    /// Scoring is a soft gradient inside the punish window (0.4 → 0.95 as the
    /// hero closes on trigger range), so the brain has a continuous reason to
    /// approach instead of a binary cliff at the range boundary. The
    /// one-hit-per-window rule is enforced by HeroBrain, not here.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(HeroEnemy))]
    public sealed class HeroDashStrike : MonoBehaviour, IHeroAbility
    {
        [Tooltip("Whether the hero has this ability by default. A DifficultyRuntime modifier on " +
                 "Target.HeroMeleeEnabled can override this — Override op with value 0 disables, 1 enables.")]
        [SerializeField] private bool enabledByDefault = true;

        public bool IsEnabled =>
            (rt != null ? rt.Get(Target.HeroMeleeEnabled, enabledByDefault ? 1f : 0f) : (enabledByDefault ? 1f : 0f)) > 0.5f;

        [Header("Animation")]
        [Tooltip("Animator that owns the hero's clips. Auto-resolves via GetComponentInChildren if left null.")]
        [SerializeField] private Animator animator;
        [Tooltip("State name to crossfade into when the dash-strike begins. Match the state node name on your controller.")]
        [SerializeField] private string animatorStateName = "attack";
        [SerializeField, Min(0f)] private float crossfadeSeconds = 0.05f;

        public string Id => "dashStrike";
        // Busy for the entire dash window so the brain doesn't pick another
        // ability mid-commit.
        public bool IsBusy => Time.time < activeUntil;
        // Dash IS the movement — must not lock kite input. HeroEnemy reads
        // CurrentDirection / SpeedMultiplier directly while IsActive.
        public bool LocksMovement => false;

        // HeroEnemy queries these each frame while IsActive to drive movement.
        public bool IsActive => Time.time < activeUntil;
        public Vector2 CurrentDirection => currentDir;
        public float SpeedMultiplier => activeSpeedMul;

        // True when the most recent dash actually landed a hit AND the dash
        // was committed inside a boss punish window. Brain polls this each
        // frame to decide whether to mark the window consumed — random
        // out-of-window jabs and whiffs both leave the cap alone.
        public bool HitConsumedPunishWindow => hitResolved && committedInPunishWindow;

        // Two independent charges. Each has its own cooldown timer; whichever
        // is older when the hero next dashes is the one consumed. After a
        // dodge, both charges are still fresh, so the hero can dash twice
        // back-to-back to re-engage instead of walking in.
        private float charge0ReadyAt;
        private float charge1ReadyAt;
        private float activeUntil;
        private Vector2 currentDir;
        private float activeSpeedMul;
        private bool hitResolved;
        // True when the current dash was committed during a boss punish
        // window. Random/proactive dashes outside the window set this false
        // so a lucky hit doesn't burn the next window's one-hit cap.
        private bool committedInPunishWindow;
        // Next Time.time at which the proactive (out-of-window) dash becomes
        // eligible. Re-rolled after every commit so jabs feel naturally
        // spaced instead of metronomic.
        private float nextProactiveAttemptAt;
        // Tracks the IsActive edge so end-of-dash hit resolution fires
        // exactly once (when the dash transitions active → inactive).
        private bool dashActiveLastFrame;

        private HeroEnemy hero;
        private DifficultyRuntime rt;
        private BossController bossRef;
        private GridFootprint bossFootprint;
        private bool stateExists;

        private void Awake()
        {
            hero = GetComponent<HeroEnemy>();
            rt = FindFirstObjectByType<DifficultyRuntime>();
            bossRef = FindFirstObjectByType<BossController>();
            if (bossRef != null) bossFootprint = bossRef.GetComponent<GridFootprint>();
            // First proactive jab arms within the first few seconds so the
            // hero feels active from the moment of engagement.
            nextProactiveAttemptAt = Time.time + Random.Range(3f, 5f);
            if (animator == null) animator = GetComponentInChildren<Animator>(includeInactive: true);

            // Arm a generous initial cooldown here as a safety net. The real
            // re-arm happens via ArmSpawnDelay() once HeroEnemy detects the
            // gameplay (post-dialogue) state — by then the Time.time set in
            // Awake is already in the past, so the dialogue-driven delay
            // wouldn't help on its own.
            float spawnDelay = hero != null && hero.Config != null
                ? hero.Config.dashStrikeSpawnDelaySeconds
                : 1.5f;
            float ready = Time.time + Mathf.Max(0f, spawnDelay);
            charge0ReadyAt = ready;
            charge1ReadyAt = ready;
            if (animator != null && !string.IsNullOrEmpty(animatorStateName))
            {
                stateExists = animator.HasState(0, Animator.StringToHash(animatorStateName));
                if (!stateExists)
                    Debug.LogWarning($"{nameof(HeroDashStrike)}: state '{animatorStateName}' not found on " +
                                     $"animator '{animator.name}' (layer 0). Strike animation will not play.", this);
            }
        }

        // Ready if either charge has regenerated AND the hero isn't already
        // mid-dash AND the post-hit attack lockout isn't active.
        public bool IsReady => !IsActive
            && !hero.IsAttackLocked
            && (Time.time >= charge0ReadyAt || Time.time >= charge1ReadyAt);

        public float Score(in HeroDecisionContext ctx)
        {
            // Max-range gate: distance to the boss's footprint EDGE must be
            // within actual dash reach (fixed speed × max duration). Beyond
            // that, the dash physically can't land and the kite-steering
            // closes the gap first instead of letting the hero whiff a
            // half-step lunge.
            float distToEdge = DistanceToBossEdge(ctx.heroCenter);
            if (distToEdge > MaxDashReachCells()) return 0f;

            // Punish window: top-priority commit. Pacing comes from the
            // cooldown + the brain's one-HIT-per-window rule (NOT
            // one-commit-per-window — whiffs don't burn the opening, so the
            // hero gets to try again instead of giving up after one miss).
            if (ctx.bossInPunishWindow) return 0.95f;
            // Proactive jab: every ~3-5s, take a random swing even outside a
            // punish window. Skipped when the boss is invulnerable (e.g.
            // mid-windup of the J slash, or charge-slam trail) — the hit
            // would silently no-op and the jab would feel like the hero just
            // tapped the boss and walked away.
            if (Time.time >= nextProactiveAttemptAt
                && bossRef != null && !bossRef.IsInvulnerable)
                return 0.55f;
            return 0f;
        }

        // Distance (cells) the dash travels at max duration. Public so the
        // kite steering can park the hero just inside this range — that way
        // the punish-window approach phase ends at "dash distance", not
        // pressed against the boss.
        public float MaxDashReachCells()
        {
            float baseline = hero.Config.moveSpeed * Mathf.Max(0.01f, hero.Config.moveSpeedMultiplier);
            float speed = Eff(Target.HeroMoveSpeed, baseline) * Mathf.Max(1f, hero.Config.dashStrikeSpeedMultiplier);
            return speed * hero.Config.dashStrikeDurationSeconds;
        }

        // Cell-radius the hero should kite to during the punish window — far
        // enough for the dash to look like a real lunge, close enough that
        // the dash always lands. Includes an approximate boss radius so the
        // kite point sits at "edge of dash range" relative to the boss
        // footprint, not the boss center.
        public float PunishEngagementDistanceCells()
        {
            float bossRadius = 1.75f;
            if (bossFootprint != null)
            {
                Vector2 fp = bossFootprint.Footprint;
                bossRadius = 0.5f * Mathf.Max(fp.x, fp.y);
            }
            // Leave a 0.5-cell buffer inside max reach so grid snap doesn't
            // park the hero just outside the score gate.
            return Mathf.Max(1f, MaxDashReachCells() + bossRadius - 0.5f);
        }

        public void Begin(in HeroDecisionContext ctx)
        {
            if (animator != null && stateExists)
                animator.CrossFadeInFixedTime(animatorStateName, crossfadeSeconds);

            // Fixed-speed dash with a duration scaled to the current gap.
            // Speed is constant (dashStrikeSpeedMultiplier × move speed) so
            // every dash reads as a real lunge; duration shrinks so the dash
            // naturally ends AT the boss edge instead of overshooting and
            // bouncing off grid collision. Beyond MaxDashReachCells the
            // Score gate already blocked the commit, so duration here is
            // bounded by the configured cap.
            activeSpeedMul = hero.Config.dashStrikeSpeedMultiplier;
            float maxDur = hero.Config.dashStrikeDurationSeconds;
            float minDur = hero.Config.dashStrikeMinDurationSeconds;
            {
                Vector2 heroCenterNow = hero.Footprint.Anchor + hero.Footprint.Footprint * 0.5f;
                float distToEdge = DistanceToBossEdge(heroCenterNow);
                float baseline = hero.Config.moveSpeed * Mathf.Max(0.01f, hero.Config.moveSpeedMultiplier);
                float effSpeed = Eff(Target.HeroMoveSpeed, baseline);
                float dashSpeed = effSpeed * activeSpeedMul;
                float arriveDur = dashSpeed > 0.0001f ? distToEdge / dashSpeed : maxDur;
                activeUntil = Time.time + Mathf.Clamp(arriveDur, minDur, maxDur);
            }
            // Two-charge cooldown: consume whichever charge has been ready
            // longest (oldest readyAt). The other charge stays available for
            // an immediate follow-up dash. Cooldown starts on cast so a whiff
            // still respects the gap before the next attempt on this charge.
            float cooldown = Eff(Target.HeroMeleeCooldownSeconds, hero.Config.dashStrikeCooldownSeconds);
            if (charge0ReadyAt <= charge1ReadyAt) charge0ReadyAt = Time.time + cooldown;
            else                                  charge1ReadyAt = Time.time + cooldown;
            hitResolved = false;
            committedInPunishWindow = ctx.bossInPunishWindow;
            // Re-roll the proactive timer so any commit (punish or random)
            // pushes the next free jab 3-5s out.
            nextProactiveAttemptAt = Time.time + Random.Range(3f, 5f);
            currentDir = ComputeDirectionTowardBoss(ctx.heroCenter, ctx.bossCenter);
        }

        public void Tick(float dt)
        {
            bool active = IsActive;

            // Detect the active → inactive transition so the hit resolves
            // exactly once at the end of the dash. The dash always plays out
            // for its full duration first — without this, a hero already in
            // range when the dash starts would land the hit on frame 1 and
            // the dash would visually look like an instant tap.
            if (dashActiveLastFrame && !active)
            {
                ResolveHitAtDashEnd();
            }
            dashActiveLastFrame = active;

            if (!active) return;

            // Recompute direction each frame so a strafing boss can't fully
            // evade by moving perpendicular during the dash window.
            Vector2 heroCenter = hero.Footprint.Anchor + hero.Footprint.Footprint * 0.5f;
            Vector2 bossCenter = GetBossCenter();
            currentDir = ComputeDirectionTowardBoss(heroCenter, bossCenter);
        }

        // End-of-dash range check + damage application. Damage is measured to
        // the boss's footprint EDGE so a large boss footprint can't keep the
        // hero geometrically outside hit range. If the boss is invulnerable
        // at the moment of resolution (e.g. mid-windup of a chained attack),
        // TryResolveHit returns false; the dash is over either way, but the
        // punish-window cap stays unspent so the hero gets another attempt
        // on the next charge.
        private void ResolveHitAtDashEnd()
        {
            // Always arm a short post-attack lockout so even a whiffed dash
            // forces a pause before the next swing. TryResolveHit will extend
            // this with the longer post-hit lockout when damage actually
            // landed (LockoutAttacksFor only ever extends).
            if (hero != null)
                hero.LockoutAttacksFor(hero.Config.dashStrikePostMissLockoutSeconds);

            if (hitResolved) return;
            Vector2 heroCenter = hero.Footprint.Anchor + hero.Footprint.Footprint * 0.5f;
            float range = Eff(Target.HeroMeleeRangeCells, hero.Config.dashStrikeMeleeRangeCells);
            float distToEdge = DistanceToBossEdge(heroCenter);
            if (distToEdge <= range && TryResolveHit())
                hitResolved = true;
        }

        // Axis-aligned distance from a point to the boss's footprint rect.
        // Returns 0 if the point is inside the rect, otherwise the distance
        // to the nearest edge.
        private float DistanceToBossEdge(Vector2 point)
        {
            if (bossFootprint == null) return float.PositiveInfinity;
            Vector2 anchor = bossFootprint.Anchor;
            Vector2 size = bossFootprint.Footprint;
            float dx = Mathf.Max(0f, Mathf.Max(anchor.x - point.x, point.x - (anchor.x + size.x)));
            float dy = Mathf.Max(0f, Mathf.Max(anchor.y - point.y, point.y - (anchor.y + size.y)));
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        public void Cancel()
        {
            charge0ReadyAt = Time.time;
            charge1ReadyAt = Time.time;
            activeUntil = 0f;
            hitResolved = false;
        }

        /// <summary>
        /// (Re)arm both charges with the spawn delay measured from now.
        /// Called by HeroEnemy on the dialogue → Playing transition so the
        /// hero can't insta-attack the moment the cutscene clears.
        /// </summary>
        public void ArmSpawnDelay()
        {
            float spawnDelay = hero != null && hero.Config != null
                ? hero.Config.dashStrikeSpawnDelaySeconds
                : 1.5f;
            float ready = Time.time + Mathf.Max(0f, spawnDelay);
            charge0ReadyAt = ready;
            charge1ReadyAt = ready;
            // Also push out the next proactive jab so the hero doesn't immediately
            // free-fire something else either.
            nextProactiveAttemptAt = Time.time + Mathf.Max(spawnDelay, Random.Range(3f, 5f));
        }

        private Vector2 ComputeDirectionTowardBoss(Vector2 heroCenter, Vector2 bossCenter)
        {
            Vector2 d = bossCenter - heroCenter;
            return d.sqrMagnitude > 0.0001f ? d.normalized : Vector2.right;
        }

        private Vector2 GetBossCenter()
        {
            // Lazy re-resolve in case the original boss instance was destroyed
            // and replaced (rare, but the scene-reload path can swap refs).
            if (bossFootprint == null)
            {
                if (bossRef == null) bossRef = FindFirstObjectByType<BossController>();
                if (bossRef != null) bossFootprint = bossRef.GetComponent<GridFootprint>();
            }
            if (bossFootprint == null) return Vector2.zero;
            return bossFootprint.Anchor + bossFootprint.Footprint * 0.5f;
        }

        // Returns true only if damage was actually applied. Invuln boss → false,
        // so Tick treats the contact as "not yet resolved" and keeps the dash
        // alive against the boss instead of declaring victory and bouncing off.
        private bool TryResolveHit()
        {
            if (bossRef == null) bossRef = FindFirstObjectByType<BossController>();
            if (bossRef == null) return false;
            if (bossRef.IsInvulnerable) return false;
            int damage = EffI(Target.HeroMeleeDamage, hero.Config.dashStrikeDamage);
            bossRef.TakeDamage(damage, hero);
            // Breathing-room lockout: after the hero successfully connects,
            // gate all offensive abilities (dash-strike + fireball) for
            // dashStrikePostHitLockoutSeconds so the player isn't immediately
            // re-pressured between hits.
            hero.LockoutAttacksFor(hero.Config.dashStrikePostHitLockoutSeconds);
            return true;
        }

        private float Eff(Target t, float b) => rt != null ? rt.Get(t, b) : b;
        private int   EffI(Target t, int b)  => rt != null ? rt.GetInt(t, b) : b;
    }
}
