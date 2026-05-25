using System;
using BossJam.Difficulty;
using BossJam.GridSystem;
using BossJam.Player;
using UnityEngine;

namespace BossJam.Attacks
{
    /// <summary>
    /// Quick AoE in front of the boss. Composes an AttackStateMachine; subscribes
    /// per-attack behavior to its phase events. No inheritance.
    /// </summary>
    [DisallowMultipleComponent]
    public class GroundSlashAttack : MonoBehaviour, IAttack, ITickScalable
    {
        [SerializeField] private AttackConfig config;

        [SerializeField, Tooltip("If true, Strike() fires automatically at the start of Active. " +
                                 "Set false to drive Strike() from an AnimationEvent for frame-precise damage.")]
        private bool autoStrikeOnActive = true;

        private readonly AttackStateMachine fsm = new AttackStateMachine();
        private Vector3 lastAim;
        private Vector2 lastAimDirection = Vector2.right; // fallback if no live boss aim
        private GameObject liveTelegraph;
        private GameObject liveHitbox;
        private bool hasStruck;
        private GridFootprint cachedBossFootprint;
        private BossController cachedBoss;

        private DifficultyRuntime rt;
        private float Eff(Target t, float b, string ext = null)
            => rt != null ? rt.Get(t, b, config != null ? config.id : null, ext) : b;
        private int EffI(Target t, int b, string ext = null)
            => rt != null ? rt.GetInt(t, b, config != null ? config.id : null, ext) : b;
        private PhaseTimings BuildTimings() => new PhaseTimings
        {
            windupSeconds              = Eff(Target.BossAttackWindupSeconds,    config.windupSeconds),
            activeSeconds              = Eff(Target.BossAttackActiveSeconds,    config.activeSeconds),
            recoverySeconds            = Eff(Target.BossAttackRecoverySeconds,  config.recoverySeconds),
            cooldownSeconds            = Eff(Target.BossAttackCooldownSeconds,  config.cooldownSeconds),
            lockMovementDuringWindup   = config.lockMovementDuringWindup,
            lockMovementDuringActive   = config.lockMovementDuringActive,
            lockMovementDuringRecovery = config.lockMovementDuringRecovery,
        };
        private Vector2 EffHitboxFootprint() => new Vector2(
            Eff(Target.BossAttackHitboxFootprintX, config.hitboxFootprint.x),
            Eff(Target.BossAttackHitboxFootprintY, config.hitboxFootprint.y));
        private float EffHitboxForwardOffset() =>
            Eff(Target.BossAttackHitboxForwardOffsetCells, config.hitboxForwardOffsetCells);

        // ---- IAttack forwarders ----
        public AttackConfig Config => config;
        public AttackState State => fsm.State;
        public float PhaseProgress01 => fsm.PhaseProgress01;
        public float CooldownRemaining => fsm.CooldownRemaining;
        public float TimeToIdle => fsm.TimeToIdle;
        public bool IsBusy => fsm.IsBusy;
        public bool LocksMovement => fsm.LocksMovement;
        public event Action<AttackState, AttackState> StateChanged
        {
            add    { fsm.StateChanged += value; }
            remove { fsm.StateChanged -= value; }
        }

        public bool TryStart(Vector3 aimWorldPoint)
        {
            if (config == null) return false;
            lastAim = aimWorldPoint;
            var bossWorld = transform.parent != null ? transform.parent.position : transform.position;
            var d = aimWorldPoint - bossWorld;
            var d2 = new Vector2(d.x, d.z);
            lastAimDirection = d2.sqrMagnitude < 0.0001f ? Vector2.right : d2.normalized;
            fsm.Init(BuildTimings());
            rt?.RaiseAttackStarted(config);
            // Per-swing reset: the AnimationEvent is the source of truth for when
            // damage lands, and it may fire during Windup (before OnActiveEnter
            // resets the latch). Without this, a Windup-time event on swing N+1
            // sees hasStruck==true from swing N and bails silently.
            hasStruck = false;
            return fsm.TryStart();
        }

        public void Cancel()
        {
            DestroyLive();
            fsm.Cancel();
        }

        public void ApplyTick(float m) => fsm.SetTickScale(m);

        // ---- Lifecycle ----
        private void Awake()
        {
            rt = FindFirstObjectByType<DifficultyRuntime>();
            if (config != null) fsm.Init(BuildTimings());
            fsm.OnEnter(AttackState.Windup,   OnWindupEnter);
            fsm.OnEnter(AttackState.Active,   OnActiveEnter);
            fsm.OnEnter(AttackState.Recovery, DestroyHitbox);
            fsm.OnEnter(AttackState.Idle,     DestroyLive);
        }

        private void Update()
        {
            fsm.Tick(Time.deltaTime);
            // Refresh aim from the boss each frame so the telegraph (and the eventual
            // Strike) reflect the player's current facing. The hitbox itself does NOT
            // follow — once Strike() spawns it, it stays put. Sliding it under the
            // hero used to retrigger overlap-entry damage on aim wobble.
            RefreshAimFromBoss();
            if (fsm.State == AttackState.Windup) UpdateTelegraphFollowBoss();
        }

        private void RefreshAimFromBoss()
        {
            var boss = BossControllerRef;
            if (boss == null) return;
            var fwd = boss.AimForward;
            var d2 = new Vector2(fwd.x, fwd.z);
            if (d2.sqrMagnitude > 0.0001f) lastAimDirection = d2.normalized;
        }

        private void UpdateTelegraphFollowBoss()
        {
            if (liveTelegraph == null) return;
            var grid = BossFootprint != null ? BossFootprint.Grid : null;
            if (grid == null) return;

            var fpSize = EffHitboxFootprint();
            var anchor = ClampAnchor(HitboxAnchor(fpSize), fpSize);
            liveTelegraph.transform.position = grid.FootprintCenterWorld(anchor, fpSize);
            // Keep the hazard rect glued to the telegraph each frame so hero
            // perception sees it move with the boss.
            var hazard = liveTelegraph.GetComponent<Hazard>();
            if (hazard != null) hazard.Configure(anchor, fpSize);
        }

        private void OnDisable() => DestroyLive();

        // ---- Internals ----
        private GridFootprint BossFootprint =>
            cachedBossFootprint != null
                ? cachedBossFootprint
                : (cachedBossFootprint = GetComponentInParent<GridFootprint>());

        private BossController BossControllerRef =>
            cachedBoss != null
                ? cachedBoss
                : (cachedBoss = GetComponentInParent<BossController>());

        private Vector2 HitboxAnchor(Vector2 fpSize)
        {
            var fp = BossFootprint;
            var bossCenter = fp.Anchor + fp.Footprint * 0.5f;
            var hbCenter = bossCenter + lastAimDirection * EffHitboxForwardOffset();
            return hbCenter - fpSize * 0.5f;
        }

        // Keep the anchor's footprint fully inside the grid. Without this, near-edge
        // spawns fail Register() silently and the hitbox never deals damage.
        private Vector2 ClampAnchor(Vector2 anchor, Vector2 fpSize)
        {
            var grid = BossFootprint != null ? BossFootprint.Grid : null;
            if (grid == null) return anchor;
            float maxX = Mathf.Max(0f, grid.Width  - fpSize.x);
            float maxY = Mathf.Max(0f, grid.Height - fpSize.y);
            return new Vector2(
                Mathf.Clamp(anchor.x, 0f, maxX),
                Mathf.Clamp(anchor.y, 0f, maxY));
        }

        private void SpawnTelegraph()
        {
            if (config == null || config.telegraphPrefab == null) return;
            var grid = BossFootprint != null ? BossFootprint.Grid : null;
            if (grid == null) return;

            var fpSize = EffHitboxFootprint();
            var anchor = ClampAnchor(HitboxAnchor(fpSize), fpSize);
            var world = grid.FootprintCenterWorld(anchor, fpSize);
            liveTelegraph = Instantiate(config.telegraphPrefab, world, Quaternion.identity);
            BossJam.Game.DebugVisualHider.ApplyTo(liveTelegraph);
            var vis = liveTelegraph.transform.Find("Visual");
            if (vis != null)
            {
                var s = grid.CellSize * fpSize;
                vis.localScale = new Vector3(s.x, s.y, 1f);
            }
            var hazard = liveTelegraph.GetComponent<Hazard>() ?? liveTelegraph.AddComponent<Hazard>();
            hazard.Configure(anchor, fpSize);
        }

        // Boss is invulnerable from windup start until the slash keyframe
        // (Strike() — called via AnimationEvent or the autoStrikeOnActive
        // fallback). After the keyframe, recovery + cooldown are open for the
        // hero to punish.
        //
        // Generously over-armed (windup + active) so animation event timing
        // wobble can't leave a frame-sized vulnerable gap before the keyframe
        // fires. Strike() force-ends the window the moment damage actually
        // commits.
        private void OnWindupEnter()
        {
            SpawnTelegraph();
            var boss = BossControllerRef;
            if (boss == null || config == null) return;
            float windupSec = Eff(Target.BossAttackWindupSeconds, config.windupSeconds);
            float activeSec = Eff(Target.BossAttackActiveSeconds, config.activeSeconds);
            boss.SetInvulnFor(windupSec + activeSec);
        }

        private void OnActiveEnter()
        {
            DestroyTelegraph();
            if (autoStrikeOnActive) Strike();
        }

        /// <summary>
        /// Deal the slash damage at the current aim. Idempotent within a single
        /// attack instance — only the first call during Active spawns a hitbox.
        /// Call from a Unity AnimationEvent (see <see cref="GroundSlashStrikeRelay"/>)
        /// for frame-precise timing, or leave <c>autoStrikeOnActive</c> = true to
        /// fire at the start of the Active phase.
        ///
        /// The spawned hitbox is stationary — it does NOT follow the boss. It
        /// lives for the remainder of the Active phase as a VFX window, then
        /// the FSM destroys it on Recovery.
        /// </summary>
        public void Strike()
        {
            // The animation drives damage timing. The clip's strike event can land
            // a frame outside the nominal Active window due to animator/Update
            // ordering — gating on fsm.State==Active caused first-swing whiffs
            // when the event fired right at the Active→Recovery boundary. Allow
            // any non-Idle/Cooldown state; the hasStruck latch + TryStart reset
            // still enforce once-per-swing.
            if (fsm.State == AttackState.Idle || fsm.State == AttackState.Cooldown) return;
            if (hasStruck) return;
            hasStruck = true;
            SpawnHitboxAtCurrentAim();
            // Keyframe-aligned vulnerability: the boss commits to the swing
            // here, so i-frames from OnWindupEnter end now. Hero can punish
            // through recovery/cooldown.
            var boss = BossControllerRef;
            if (boss != null) boss.ClearInvuln();
        }

        private void SpawnHitboxAtCurrentAim()
        {
            if (config == null || config.hitboxPrefab == null) return;
            var grid = BossFootprint != null ? BossFootprint.Grid : null;
            if (grid == null) return;

            var fpSize = EffHitboxFootprint();
            var anchor = ClampAnchor(HitboxAnchor(fpSize), fpSize);
            var go = Instantiate(config.hitboxPrefab);
            go.SetActive(false);

            var fp = go.GetComponent<GridFootprint>();
            if (fp != null) fp.Configure(anchor, fpSize, grid);

            var hazard = go.GetComponent<Hazard>() ?? go.AddComponent<Hazard>();
            hazard.Configure(anchor, fpSize);

            var hb = go.GetComponent<AttackHitbox>();
            if (hb != null) hb.SetDamage(EffI(Target.BossAttackDamage, config.damage));

            go.transform.position = grid.FootprintCenterWorld(anchor, fpSize);

            var vis = go.transform.Find("Visual");
            if (vis != null)
            {
                var s = grid.CellSize * fpSize;
                vis.localScale = new Vector3(s.x, vis.localScale.y, s.y);
            }

            go.SetActive(true);
            liveHitbox = go;
        }

        private void DestroyTelegraph()
        {
            if (liveTelegraph != null) { Destroy(liveTelegraph); liveTelegraph = null; }
        }

        private void DestroyHitbox()
        {
            if (liveHitbox != null) { Destroy(liveHitbox); liveHitbox = null; }
        }

        private void DestroyLive()
        {
            DestroyTelegraph();
            DestroyHitbox();
        }
    }
}
