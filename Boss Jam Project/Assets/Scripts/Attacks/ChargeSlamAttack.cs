using System;
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
        private float tickScale = 1f;

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
            lastAim = aimWorldPoint;
            chargeDirection = AimDirectionFromBoss();
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
            fsm.Init(config);
            fsm.OnEnterWindup    += SpawnTelegraph;
            fsm.OnEnterRecovery  += SpawnHitboxAndClearTelegraph;
            fsm.OnEnterCooldown  += DestroyHitbox;
            fsm.OnEnterIdle      += DestroyLive;
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
            var delta = chargeDirection * (config.chargeCellsPerSecond * tickScale * Time.deltaTime);
            var target = mover.AnchorPosition + delta;

            // Try the combined move; if blocked, axis-split to slide along walls.
            if (!mover.DriveTo(target))
            {
                mover.DriveTo(new Vector2(target.x, mover.AnchorPosition.y));
                mover.DriveTo(new Vector2(mover.AnchorPosition.x, target.y));
            }
        }

        // Hitbox spawns at boss's CURRENT center (after charge has moved them).
        private Vector2 HitboxAnchorAtBoss()
        {
            var fp = BossFootprint;
            var bossCenter = fp.Anchor + fp.Footprint * 0.5f;
            return bossCenter - config.hitboxFootprint * 0.5f;
        }

        // Telegraph shows the PROJECTED end position based on charge speed × active duration.
        private Vector2 TelegraphAnchor()
        {
            var fp = BossFootprint;
            var estDistance = config.chargeCellsPerSecond * config.activeSeconds * tickScale;
            var bossCenter = fp.Anchor + fp.Footprint * 0.5f;
            var endCenter = bossCenter + chargeDirection * estDistance;
            return endCenter - config.hitboxFootprint * 0.5f;
        }

        private void SpawnTelegraph()
        {
            if (config == null || config.telegraphPrefab == null) return;
            var grid = BossFootprint != null ? BossFootprint.Grid : null;
            if (grid == null) return;

            var anchor = TelegraphAnchor();
            var world = grid.FootprintCenterWorld(anchor, config.hitboxFootprint);
            liveTelegraph = Instantiate(config.telegraphPrefab, world, Quaternion.identity);
            var vis = liveTelegraph.transform.Find("Visual");
            if (vis != null)
            {
                var s = grid.CellSize * config.hitboxFootprint;
                vis.localScale = new Vector3(s.x, s.y, 1f);
            }
        }

        private void SpawnHitboxAndClearTelegraph()
        {
            DestroyTelegraph();
            if (config == null || config.hitboxPrefab == null) return;
            var grid = BossFootprint != null ? BossFootprint.Grid : null;
            if (grid == null) return;

            var anchor = HitboxAnchorAtBoss();
            var go = Instantiate(config.hitboxPrefab);
            go.SetActive(false);

            var fp = go.GetComponent<GridFootprint>();
            if (fp != null) fp.Configure(anchor, config.hitboxFootprint, grid);

            var hb = go.GetComponent<AttackHitbox>();
            if (hb != null) hb.SetDamage(config.damage);

            go.transform.position = grid.FootprintCenterWorld(anchor, config.hitboxFootprint);

            var vis = go.transform.Find("Visual");
            if (vis != null)
            {
                var s = grid.CellSize * config.hitboxFootprint;
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
