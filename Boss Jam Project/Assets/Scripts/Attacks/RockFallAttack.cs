using System;
using System.Collections.Generic;
using BossJam.Difficulty;
using BossJam.GridSystem;
using UnityEngine;

namespace BossJam.Attacks
{
    /// <summary>
    /// Ult: rocks rain down at random in-bounds cells across the Active phase, one at a
    /// time on a jittered schedule. Each rock has its own warning telegraph → falling
    /// visual → damage hitbox lifecycle, independent from sibling rocks. Player keeps
    /// moving across the arena to dodge staggered impacts.
    ///
    /// Composes an AttackStateMachine. The FSM phases bound the rain window:
    ///   Windup   — boss casts (animation only)
    ///   Active   — schedule fires; new rocks keep spawning
    ///   Recovery — schedule's tail still firing; in-flight rocks finish their cycles
    ///   Cooldown — re-cast locked out
    /// </summary>
    [DisallowMultipleComponent]
    public class RockFallAttack : MonoBehaviour, IAttack, ITickScalable
    {
        [SerializeField] private AttackConfig config;

        private readonly AttackStateMachine fsm = new AttackStateMachine();
        private readonly List<LiveRock> liveRocks = new List<LiveRock>();
        private float[] spawnTimes;
        private int nextSpawnIndex;
        private float activePhaseStartTime;
        private GridFootprint cachedBossFootprint;
        private BossGrid cachedGrid;
        private float tickScale = 1f;

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
            fsm.OnEnter(AttackState.Active, BuildSpawnSchedule);
            fsm.OnEnter(AttackState.Idle,   DestroyLive);
        }

        private void Update()
        {
            fsm.Tick(Time.deltaTime);

            // Fire scheduled spawns. Bounded by count, not by phase — trailing entries
            // from a busy Active tail still spawn even after the FSM has moved to Recovery.
            if (spawnTimes != null && nextSpawnIndex < spawnTimes.Length)
            {
                var elapsed = Time.time - activePhaseStartTime;
                while (nextSpawnIndex < spawnTimes.Length && elapsed >= spawnTimes[nextSpawnIndex])
                {
                    SpawnRock();
                    nextSpawnIndex++;
                }
            }

            UpdateLiveRocks();
        }

        private void OnDisable() => DestroyLive();

        // ---- Internals ----
        private GridFootprint BossFootprint =>
            cachedBossFootprint != null
                ? cachedBossFootprint
                : (cachedBossFootprint = GetComponentInParent<GridFootprint>());

        private BossGrid Grid =>
            cachedGrid != null
                ? cachedGrid
                : (cachedGrid = BossFootprint != null ? BossFootprint.Grid : null);

        private void BuildSpawnSchedule()
        {
            activePhaseStartTime = Time.time;
            nextSpawnIndex = 0;

            int n = Mathf.Max(1, config != null
                ? EffI(Target.AttackExtension, config.spawnCount, ext: "spawnCount")
                : 1);
            spawnTimes = new float[n];

            var activeDur = (config != null
                ? Eff(Target.BossAttackActiveSeconds, config.activeSeconds)
                : 0f) * tickScale;
            var step = n > 0 ? activeDur / n : 0f;
            var jitter = config != null
                ? Eff(Target.AttackExtension, config.spawnTimeJitter, ext: "spawnTimeJitter") * tickScale
                : 0f;

            for (int i = 0; i < n; i++)
            {
                var baseTime = i * step;
                var offset = UnityEngine.Random.Range(-jitter, jitter);
                spawnTimes[i] = Mathf.Clamp(baseTime + offset, 0f, activeDur);
            }
            // Sort so we can fire in order without missing earlier entries that got pushed late by jitter.
            System.Array.Sort(spawnTimes);
        }

        private void SpawnRock()
        {
            if (Grid == null || config == null) return;
            var fp = EffHitboxFootprint();
            float maxX = Mathf.Max(0f, Grid.Width  - fp.x);
            float maxY = Mathf.Max(0f, Grid.Height - fp.y);
            var anchor = new Vector2(UnityEngine.Random.Range(0f, maxX), UnityEngine.Random.Range(0f, maxY));
            var worldImpact = Grid.FootprintCenterWorld(anchor, fp);

            var telegraph = SpawnTelegraph(worldImpact, fp, anchor);
            var fallingRock = SpawnFallingRock(worldImpact);

            var now = Time.time;
            var telegraphDur = Eff(Target.AttackExtension, config.perSpawnTelegraphSeconds, ext: "perSpawnTelegraphSeconds") * tickScale;
            var hitboxDur    = Eff(Target.AttackExtension, config.perSpawnHitboxSeconds,    ext: "perSpawnHitboxSeconds")    * tickScale;
            liveRocks.Add(new LiveRock
            {
                anchor       = anchor,
                worldImpact  = worldImpact,
                spawnTime    = now,
                impactTime   = now + telegraphDur,
                endTime      = now + telegraphDur + hitboxDur,
                telegraph    = telegraph,
                fallingRock  = fallingRock,
                hasImpacted  = false,
            });
        }

        private GameObject SpawnTelegraph(Vector3 worldImpact, Vector2 fp, Vector2 anchor)
        {
            if (config.telegraphPrefab == null) return null;
            var t = Instantiate(config.telegraphPrefab, worldImpact, Quaternion.identity);
            var vis = t.transform.Find("Visual");
            if (vis != null)
            {
                var s = Grid.CellSize * fp;
                vis.localScale = new Vector3(s.x, s.y, 1f);
            }
            // Publish the impact rect as a Hazard so the hero AI avoids it.
            var hazard = t.GetComponent<Hazard>() ?? t.AddComponent<Hazard>();
            hazard.Configure(anchor, fp);
            return t;
        }

        private GameObject SpawnFallingRock(Vector3 worldImpact)
        {
            if (config.fallingRockPrefab == null) return null;
            float fallH = Eff(Target.AttackExtension, config.fallStartHeight, ext: "fallStartHeight");
            var spawnPos = worldImpact + new Vector3(0f, fallH, 0f);
            return Instantiate(config.fallingRockPrefab, spawnPos, Quaternion.identity);
        }

        private void UpdateLiveRocks()
        {
            var now = Time.time;
            for (int i = liveRocks.Count - 1; i >= 0; i--)
            {
                var r = liveRocks[i];

                if (!r.hasImpacted)
                {
                    // Lerp the falling visual from start height down to impact, ease-in cubic.
                    if (r.fallingRock != null)
                    {
                        var tNorm = Mathf.InverseLerp(r.spawnTime, r.impactTime, now);
                        var eased = tNorm * tNorm * tNorm;
                        float fallH = Eff(Target.AttackExtension, config.fallStartHeight, ext: "fallStartHeight");
                        var startPos = r.worldImpact + new Vector3(0f, fallH, 0f);
                        r.fallingRock.transform.position = Vector3.Lerp(startPos, r.worldImpact, eased);
                    }

                    if (now >= r.impactTime)
                    {
                        if (r.telegraph != null) { Destroy(r.telegraph); r.telegraph = null; }
                        if (r.fallingRock != null) { Destroy(r.fallingRock); r.fallingRock = null; }
                        r.hitbox = SpawnHitbox(r.anchor, r.worldImpact);
                        r.hasImpacted = true;
                    }
                }
                else if (now >= r.endTime)
                {
                    if (r.hitbox != null) Destroy(r.hitbox);
                    liveRocks.RemoveAt(i);
                    continue;
                }

                liveRocks[i] = r;
            }
        }

        private GameObject SpawnHitbox(Vector2 anchor, Vector3 worldImpact)
        {
            if (config.hitboxPrefab == null) return null;
            var fp = EffHitboxFootprint();
            var go = Instantiate(config.hitboxPrefab);
            go.SetActive(false);

            var hbFootprint = go.GetComponent<GridFootprint>();
            if (hbFootprint != null) hbFootprint.Configure(anchor, fp, Grid);

            // Publish the hitbox rect as a Hazard alongside its GridFootprint
            // so the hero AI keeps avoiding the impact zone post-impact.
            var hazard = go.GetComponent<Hazard>() ?? go.AddComponent<Hazard>();
            hazard.Configure(anchor, fp);

            var hb = go.GetComponent<AttackHitbox>();
            if (hb != null) hb.SetDamage(EffI(Target.BossAttackDamage, config.damage));

            go.transform.position = worldImpact;
            var vis = go.transform.Find("Visual");
            if (vis != null)
            {
                var s = Grid.CellSize * fp;
                vis.localScale = new Vector3(s.x, vis.localScale.y, s.y);
            }

            go.SetActive(true);
            return go;
        }

        private void DestroyLive()
        {
            for (int i = 0; i < liveRocks.Count; i++)
            {
                var r = liveRocks[i];
                if (r.telegraph != null) Destroy(r.telegraph);
                if (r.fallingRock != null) Destroy(r.fallingRock);
                if (r.hitbox != null) Destroy(r.hitbox);
            }
            liveRocks.Clear();
            spawnTimes = null;
            nextSpawnIndex = 0;
        }

        // ---- Per-rock entry ----
        private struct LiveRock
        {
            public Vector2 anchor;
            public Vector3 worldImpact;
            public float spawnTime;
            public float impactTime;
            public float endTime;
            public GameObject telegraph;
            public GameObject fallingRock;
            public GameObject hitbox;
            public bool hasImpacted;
        }
    }
}
