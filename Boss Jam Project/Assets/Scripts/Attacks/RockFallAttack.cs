using System;
using System.Collections.Generic;
using BossJam.Difficulty;
using BossJam.GridSystem;
using UnityEngine;
using DG.Tweening;

namespace BossJam.Attacks
{
    [DisallowMultipleComponent]
    public class RockFallAttack : MonoBehaviour, IAttack, ITickScalable
    {
        [SerializeField] private AttackConfig config;
        [SerializeField] private Ease rockFallEase = Ease.InCubic;

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
            windupSeconds = Eff(Target.BossAttackWindupSeconds, config.windupSeconds),
            activeSeconds = Eff(Target.BossAttackActiveSeconds, config.activeSeconds),
            recoverySeconds = Eff(Target.BossAttackRecoverySeconds, config.recoverySeconds),
            cooldownSeconds = Eff(Target.BossAttackCooldownSeconds, config.cooldownSeconds),
            lockMovementDuringWindup = config.lockMovementDuringWindup,
            lockMovementDuringActive = config.lockMovementDuringActive,
            lockMovementDuringRecovery = config.lockMovementDuringRecovery,
        };

        private Vector2 EffHitboxFootprint() => new Vector2(
            Eff(Target.BossAttackHitboxFootprintX, config.hitboxFootprint.x),
            Eff(Target.BossAttackHitboxFootprintY, config.hitboxFootprint.y)
        );

        public AttackConfig Config => config;
        public AttackState State => fsm.State;
        public float PhaseProgress01 => fsm.PhaseProgress01;
        public float CooldownRemaining => fsm.CooldownRemaining;
        public float TimeToIdle => fsm.TimeToIdle;
        public bool IsBusy => fsm.IsBusy;
        public bool LocksMovement => fsm.LocksMovement;

        public event Action<AttackState, AttackState> StateChanged
        {
            add { fsm.StateChanged += value; }
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

        private void Awake()
        {
            rt = FindFirstObjectByType<DifficultyRuntime>();

            if (config != null)
                fsm.Init(BuildTimings());

            fsm.OnEnter(AttackState.Active, BuildSpawnSchedule);
            fsm.OnEnter(AttackState.Idle, DestroyLive);
        }

        private void Update()
        {
            fsm.Tick(Time.deltaTime);

            if (spawnTimes != null && nextSpawnIndex < spawnTimes.Length)
            {
                float elapsed = Time.time - activePhaseStartTime;

                while (nextSpawnIndex < spawnTimes.Length && elapsed >= spawnTimes[nextSpawnIndex])
                {
                    SpawnRock();
                    nextSpawnIndex++;
                }
            }

            UpdateLiveRocks();
        }

        private void OnDisable()
        {
            DestroyLive();
        }

        private GridFootprint BossFootprint =>
            cachedBossFootprint != null
                ? cachedBossFootprint
                : cachedBossFootprint = GetComponentInParent<GridFootprint>();

        private BossGrid Grid =>
            cachedGrid != null
                ? cachedGrid
                : cachedGrid = BossFootprint != null ? BossFootprint.Grid : null;

        private void BuildSpawnSchedule()
        {
            activePhaseStartTime = Time.time;
            nextSpawnIndex = 0;

            int n = Mathf.Max(1, config != null
                ? EffI(Target.AttackExtension, config.spawnCount, ext: "spawnCount")
                : 1);

            spawnTimes = new float[n];

            float activeDur = (config != null
                ? Eff(Target.BossAttackActiveSeconds, config.activeSeconds)
                : 0f) * tickScale;

            float step = n > 0 ? activeDur / n : 0f;

            float jitter = config != null
                ? Eff(Target.AttackExtension, config.spawnTimeJitter, ext: "spawnTimeJitter") * tickScale
                : 0f;

            for (int i = 0; i < n; i++)
            {
                float baseTime = i * step;
                float offset = UnityEngine.Random.Range(-jitter, jitter);

                spawnTimes[i] = Mathf.Clamp(baseTime + offset, 0f, activeDur);
            }

            Array.Sort(spawnTimes);
        }

        private void SpawnRock()
        {
            if (Grid == null || config == null) return;

            Vector2 fp = EffHitboxFootprint();

            float maxX = Mathf.Max(0f, Grid.Width - fp.x);
            float maxY = Mathf.Max(0f, Grid.Height - fp.y);

            Vector2 anchor = new Vector2(
                UnityEngine.Random.Range(0f, maxX),
                UnityEngine.Random.Range(0f, maxY)
            );

            Vector3 worldImpact = Grid.FootprintCenterWorld(anchor, fp);

            float telegraphDur = Eff(
                Target.AttackExtension,
                config.perSpawnTelegraphSeconds,
                ext: "perSpawnTelegraphSeconds"
            ) * tickScale;

            float hitboxDur = Eff(
                Target.AttackExtension,
                config.perSpawnHitboxSeconds,
                ext: "perSpawnHitboxSeconds"
            ) * tickScale;

            GameObject telegraph = SpawnTelegraph(worldImpact, fp, anchor);
            GameObject fallingRock = SpawnFallingRock(worldImpact, telegraphDur);

            float now = Time.time;

            liveRocks.Add(new LiveRock
            {
                anchor = anchor,
                worldImpact = worldImpact,
                spawnTime = now,
                impactTime = now + telegraphDur,
                endTime = now + telegraphDur + hitboxDur,
                telegraph = telegraph,
                fallingRock = fallingRock,
                hasImpacted = false,
            });
        }

        private GameObject SpawnTelegraph(Vector3 worldImpact, Vector2 fp, Vector2 anchor)
        {
            if (config.telegraphPrefab == null) return null;

            GameObject t = Instantiate(config.telegraphPrefab, worldImpact, Quaternion.identity);

            Transform vis = t.transform.Find("Visual");

            if (vis != null)
            {
                Vector2 s = Grid.CellSize * fp;
                vis.localScale = new Vector3(s.x, s.y, 1f);
            }

            Hazard hazard = t.GetComponent<Hazard>() ?? t.AddComponent<Hazard>();
            hazard.Configure(anchor, fp);

            return t;
        }

        private GameObject SpawnFallingRock(Vector3 worldImpact, float fallDuration)
        {
            if (config.fallingRockPrefabs == null || config.fallingRockPrefabs.Length == 0)
                return null;

            GameObject rockPrefab = config.fallingRockPrefabs[
                UnityEngine.Random.Range(0, config.fallingRockPrefabs.Length)
            ];

            if (rockPrefab == null)
                return null;

            float fallH = Eff(
                Target.AttackExtension,
                config.fallStartHeight,
                ext: "fallStartHeight"
            );

            Vector3 spawnPos = worldImpact + new Vector3(0f, fallH, 0f);

            GameObject rock = Instantiate(rockPrefab, spawnPos, Quaternion.identity);

float tweenValue = 0f;

Vector3 startPos = spawnPos;

DOTween.To(
    () => tweenValue,
    x =>
    {
        tweenValue = x;

        // STEP animation to 6fps
        float stepped =
            Mathf.Floor(tweenValue * 24f) / 24f;

        stepped = Mathf.Clamp01(stepped);

        // apply easing AFTER stepping
        float eased = DOVirtual.EasedValue(
            0f,
            1f,
            stepped,
            rockFallEase
        );

        rock.transform.position = Vector3.Lerp(
            startPos,
            worldImpact,
            eased
        );
    },
    1f,
    fallDuration
)
.SetEase(Ease.Linear)
.SetLink(rock);

            return rock;
        }

        private void UpdateLiveRocks()
        {
            float now = Time.time;

            for (int i = liveRocks.Count - 1; i >= 0; i--)
            {
                LiveRock r = liveRocks[i];

                if (!r.hasImpacted)
                {
                    if (now >= r.impactTime)
                    {
                        if (r.telegraph != null)
                        {
                            Destroy(r.telegraph);
                            r.telegraph = null;
                        }

                        if (r.fallingRock != null)
                        {
                            r.fallingRock.transform.DOKill();
                            Destroy(r.fallingRock);
                            r.fallingRock = null;
                        }

                        r.hitbox = SpawnHitbox(r.anchor, r.worldImpact);
                        r.hasImpacted = true;
                    }
                }
                else if (now >= r.endTime)
                {
                    if (r.hitbox != null)
                        Destroy(r.hitbox);

                    liveRocks.RemoveAt(i);
                    continue;
                }

                liveRocks[i] = r;
            }
        }

        private GameObject SpawnHitbox(Vector2 anchor, Vector3 worldImpact)
        {
            if (config.hitboxPrefab == null) return null;

            Vector2 fp = EffHitboxFootprint();

            GameObject go = Instantiate(config.hitboxPrefab);
            go.SetActive(false);

            GridFootprint hbFootprint = go.GetComponent<GridFootprint>();

            if (hbFootprint != null)
                hbFootprint.Configure(anchor, fp, Grid);

            Hazard hazard = go.GetComponent<Hazard>() ?? go.AddComponent<Hazard>();
            hazard.Configure(anchor, fp);

            AttackHitbox hb = go.GetComponent<AttackHitbox>();

            if (hb != null)
                hb.SetDamage(EffI(Target.BossAttackDamage, config.damage));

            go.transform.position = worldImpact;

            Transform vis = go.transform.Find("Visual");

            if (vis != null)
            {
                Vector2 s = Grid.CellSize * fp;
                vis.localScale = new Vector3(s.x, vis.localScale.y, s.y);
            }

            go.SetActive(true);

            return go;
        }

        private void DestroyLive()
        {
            for (int i = 0; i < liveRocks.Count; i++)
            {
                LiveRock r = liveRocks[i];

                if (r.telegraph != null)
                    Destroy(r.telegraph);

                if (r.fallingRock != null)
                {
                    r.fallingRock.transform.DOKill();
                    Destroy(r.fallingRock);
                }

                if (r.hitbox != null)
                    Destroy(r.hitbox);
            }

            liveRocks.Clear();
            spawnTimes = null;
            nextSpawnIndex = 0;
        }

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