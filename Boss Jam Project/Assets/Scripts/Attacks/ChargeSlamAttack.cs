using System;
using BossJam.Difficulty;
using BossJam.GridSystem;
using BossJam.Player;
using UnityEngine;

namespace BossJam.Attacks
{
    /// <summary>
    /// Boss winds up briefly, then charges toward the aim point at a fixed speed during
    /// the Active phase. At the end of the charge, a hitbox spawns at the boss's actual
    /// position (which may be short of the original aim if a wall blocked the slide).
    /// Composes an AttackStateMachine; no inheritance.
    /// </summary>
    [DisallowMultipleComponent]
    public class ChargeSlamAttack : MonoBehaviour, IAttack, ITickScalable
    {
        [SerializeField] private AttackConfig config;

        private readonly AttackStateMachine fsm = new AttackStateMachine();
        private Vector3 lastAim;
        private Vector2 chargeDirection; // locked at TryStart, in cell-space
        private GameObject liveTelegraph;
        private GameObject liveHitbox;
        private GridFootprint cachedBossFootprint;
        private GridMover cachedMover;
        private BossController cachedBoss;
        private float tickScale = 1f;

        private DifficultyRuntime rt;
        private float Eff(Target t, float b, string ext = null)
            => rt != null ? rt.Get(t, b, config != null ? config.id : null, ext) : b;
        private int EffI(Target t, int b, string ext = null)
            => rt != null ? rt.GetInt(t, b, config != null ? config.id : null, ext) : b;
        // Snapshot effective timings + locks for one swing. Called per TryStart.
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
        private Vector2 EffHitboxFootprint()
        {
            if (rt == null || config == null) return config != null ? config.hitboxFootprint : Vector2.one;
            return new Vector2(
                Eff(Target.BossAttackHitboxFootprintX, config.hitboxFootprint.x),
                Eff(Target.BossAttackHitboxFootprintY, config.hitboxFootprint.y));
        }

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
            chargeDirection = AimDirectionFromBoss();
            fsm.Init(BuildTimings());
            rt?.RaiseAttackStarted(config);
            return fsm.TryStart();
        }

        public void Cancel()
        {
            DestroyLive();
            fsm.Cancel();
        }

        public void ApplyTick(float m)
        {
            tickScale = m;
            fsm.SetTickScale(m);
        }

        // ---- Lifecycle ----
        private void Awake()
        {
            rt = FindFirstObjectByType<DifficultyRuntime>();
            if (config != null) fsm.Init(BuildTimings());
            fsm.OnEnter(AttackState.Windup,   SpawnTelegraph);
            fsm.OnEnter(AttackState.Active,   ArmInvulnerability);
            fsm.OnEnter(AttackState.Recovery, SpawnHitboxAndClearTelegraph);
            fsm.OnEnter(AttackState.Cooldown, DestroyHitbox);
            fsm.OnEnter(AttackState.Idle,     DestroyLive);
        }

        private void Update()
        {
            fsm.Tick(Time.deltaTime);
            if (fsm.State == AttackState.Active) DriveCharge();
        }

        private void OnDisable() => DestroyLive();

        // ---- Internals ----
        private GridFootprint BossFootprint =>
            cachedBossFootprint != null
                ? cachedBossFootprint
                : (cachedBossFootprint = GetComponentInParent<GridFootprint>());

        private GridMover BossMover =>
            cachedMover != null
                ? cachedMover
                : (cachedMover = GetComponentInParent<GridMover>());

        private BossController BossControllerRef =>
            cachedBoss != null
                ? cachedBoss
                : (cachedBoss = GetComponentInParent<BossController>());

        // Boss is invulnerable for the whole charge (Active+Recovery, tick-scaled)
        // plus a fixed grace trail. If the charge ends early on a wall hit, the
        // already-armed window keeps protecting the boss for the buffered time.
        private void ArmInvulnerability()
        {
            var boss = BossControllerRef;
            if (boss == null || config == null) return;
            float activeSec   = Eff(Target.BossAttackActiveSeconds,   config.activeSeconds);
            float recoverySec = Eff(Target.BossAttackRecoverySeconds, config.recoverySeconds);
            boss.SetInvulnFor(activeSec * tickScale + recoverySec * tickScale + config.invulnTrailSeconds);
        }

        private Vector2 AimDirectionFromBoss()
        {
            var bossWorld = transform.parent != null ? transform.parent.position : transform.position;
            var d = lastAim - bossWorld;
            var d2 = new Vector2(d.x, d.z);
            return d2.sqrMagnitude < 0.0001f ? Vector2.right : d2.normalized;
        }

        private void DriveCharge()
        {
            var mover = BossMover;
            if (mover == null || config == null) return;
            var before = mover.AnchorPosition;

            float cps = Eff(Target.AttackExtension, config.chargeCellsPerSecond, ext: "chargeCellsPerSecond");
            var delta = chargeDirection * (cps * tickScale * Time.deltaTime);
            var target = before + delta;

            // Try the combined move; if blocked, axis-split to slide along walls.
            if (!mover.DriveTo(target))
            {
                mover.DriveTo(new Vector2(target.x, before.y));
                mover.DriveTo(new Vector2(before.x,  target.y));
            }

            // Zero net movement = wall head-on. End Active early so hitbox lands here.
            if (Vector2.SqrMagnitude(mover.AnchorPosition - before) < 0.0001f)
                fsm.AdvanceNow();
        }

        // Hitbox spawns at boss's CURRENT center (after charge has moved them).
        private Vector2 HitboxAnchorAtBoss(Vector2 fpSize)
        {
            var fp = BossFootprint;
            var bossCenter = fp.Anchor + fp.Footprint * 0.5f;
            return bossCenter - fpSize * 0.5f;
        }

        // Telegraph shows the PROJECTED end position based on charge speed × active duration.
        private Vector2 TelegraphAnchor(Vector2 fpSize)
        {
            var fp = BossFootprint;
            float cps = Eff(Target.AttackExtension, config.chargeCellsPerSecond, ext: "chargeCellsPerSecond");
            float activeSec = Eff(Target.BossAttackActiveSeconds, config.activeSeconds);
            var estDistance = cps * activeSec * tickScale;
            var bossCenter = fp.Anchor + fp.Footprint * 0.5f;
            var endCenter = bossCenter + chargeDirection * estDistance;
            return endCenter - fpSize * 0.5f;
        }

        // Keep an anchor's footprint fully inside the grid so spawns never fail Register().
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
            var anchor = ClampAnchor(TelegraphAnchor(fpSize), fpSize);
            var world = grid.FootprintCenterWorld(anchor, fpSize);
            liveTelegraph = Instantiate(config.telegraphPrefab, world, Quaternion.identity);
            var vis = liveTelegraph.transform.Find("Visual");
            if (vis != null)
            {
                var s = grid.CellSize * fpSize;
                vis.localScale = new Vector3(s.x, s.y, 1f);
            }
        }

        private void SpawnHitboxAndClearTelegraph()
        {
            DestroyTelegraph();
            if (config == null || config.hitboxPrefab == null) return;
            var grid = BossFootprint != null ? BossFootprint.Grid : null;
            if (grid == null) return;

            var fpSize = EffHitboxFootprint();
            var anchor = ClampAnchor(HitboxAnchorAtBoss(fpSize), fpSize);
            var go = Instantiate(config.hitboxPrefab);
            go.SetActive(false);

            var fp = go.GetComponent<GridFootprint>();
            if (fp != null) fp.Configure(anchor, fpSize, grid);

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
