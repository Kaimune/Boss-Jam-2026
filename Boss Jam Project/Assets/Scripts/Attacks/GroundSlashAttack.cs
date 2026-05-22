using System;
using BossJam.Difficulty;
using BossJam.GridSystem;
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

        private readonly AttackStateMachine fsm = new AttackStateMachine();
        private Vector3 lastAim;
        private Vector2 lastAimDirection = Vector2.right; // captured at TryStart, fixed for whole swing
        private GameObject liveTelegraph;
        private GameObject liveHitbox;
        private GridFootprint cachedBossFootprint;

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
            fsm.OnEnter(AttackState.Windup,   SpawnTelegraph);
            fsm.OnEnter(AttackState.Active,   SpawnHitboxAndClearTelegraph);
            fsm.OnEnter(AttackState.Recovery, DestroyHitbox);
            fsm.OnEnter(AttackState.Idle,     DestroyLive);
        }

        private void Update()
        {
            fsm.Tick(Time.deltaTime);
            if (fsm.State == AttackState.Active) UpdateHitboxFollowBoss();
        }

        private void UpdateHitboxFollowBoss()
        {
            if (liveHitbox == null) return;
            var grid = BossFootprint != null ? BossFootprint.Grid : null;
            if (grid == null) return;

            var fpSize = EffHitboxFootprint();
            var anchor = HitboxAnchor(fpSize);
            var hbFp = liveHitbox.GetComponent<GridFootprint>();
            if (hbFp == null) return;
            if (hbFp.TryMoveTo(anchor))
                liveHitbox.transform.position = grid.FootprintCenterWorld(anchor, fpSize);
        }

        private void OnDisable() => DestroyLive();

        // ---- Internals ----
        private GridFootprint BossFootprint =>
            cachedBossFootprint != null
                ? cachedBossFootprint
                : (cachedBossFootprint = GetComponentInParent<GridFootprint>());

        private Vector2 HitboxAnchor(Vector2 fpSize)
        {
            var fp = BossFootprint;
            var bossCenter = fp.Anchor + fp.Footprint * 0.5f;
            var hbCenter = bossCenter + lastAimDirection * EffHitboxForwardOffset();
            return hbCenter - fpSize * 0.5f;
        }

        private void SpawnTelegraph()
        {
            if (config == null || config.telegraphPrefab == null) return;
            var grid = BossFootprint != null ? BossFootprint.Grid : null;
            if (grid == null) return;

            var fpSize = EffHitboxFootprint();
            var anchor = HitboxAnchor(fpSize);
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
            var anchor = HitboxAnchor(fpSize);
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
