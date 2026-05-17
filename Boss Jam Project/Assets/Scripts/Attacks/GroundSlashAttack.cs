using System;
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
        private GameObject liveTelegraph;
        private GameObject liveHitbox;
        private GridFootprint cachedBossFootprint;

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
            fsm.Init(config);
            fsm.OnEnterWindup   += SpawnTelegraph;
            fsm.OnEnterActive   += SpawnHitboxAndClearTelegraph;
            fsm.OnEnterRecovery += DestroyHitbox;
            fsm.OnEnterIdle     += DestroyLive;
        }

        private void Update() => fsm.Tick(Time.deltaTime);

        private void OnDisable() => DestroyLive();

        // ---- Internals ----
        private GridFootprint BossFootprint =>
            cachedBossFootprint != null
                ? cachedBossFootprint
                : (cachedBossFootprint = GetComponentInParent<GridFootprint>());

        private Vector2 AimDirectionFromBoss()
        {
            var bossWorld = transform.parent != null ? transform.parent.position : transform.position;
            var d = lastAim - bossWorld;
            var d2 = new Vector2(d.x, d.z);
            return d2.sqrMagnitude < 0.0001f ? Vector2.right : d2.normalized;
        }

        private Vector2 HitboxAnchor()
        {
            var fp = BossFootprint;
            var dir = AimDirectionFromBoss();
            var bossCenter = fp.Anchor + fp.Footprint * 0.5f;
            var hbCenter = bossCenter + dir * config.hitboxForwardOffsetCells;
            return hbCenter - config.hitboxFootprint * 0.5f;
        }

        private void SpawnTelegraph()
        {
            if (config == null || config.telegraphPrefab == null) return;
            var grid = BossFootprint != null ? BossFootprint.Grid : null;
            if (grid == null) return;

            var anchor = HitboxAnchor();
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

            var anchor = HitboxAnchor();
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
