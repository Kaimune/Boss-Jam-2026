using BossJam.Difficulty;
using BossJam.GridSystem;
using UnityEngine;

namespace BossJam.Enemies
{
    /// <summary>
    /// Hero's ranged ability — lobs a Fireball at the perceived boss position
    /// on a cooldown. Constant-pressure: scores a steady 0.3 so the brain
    /// fires it whenever nothing higher-priority (melee window) is asking.
    ///
    /// Refactored out of HeroEnemy.TickFireball / SpawnFireball so the brain
    /// can sequence it alongside melee/dodge instead of ticking it on a
    /// hidden timer the decision layer can't see.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(HeroEnemy))]
    public sealed class HeroFireball : MonoBehaviour, IHeroAbility
    {
        [Tooltip("Whether the hero has this ability by default. A DifficultyRuntime modifier on " +
                 "Target.HeroFireballEnabled can override this — Override op with value 0 disables, 1 enables.")]
        [SerializeField] private bool enabledByDefault = false;

        public bool IsEnabled =>
            (rt != null ? rt.Get(Target.HeroFireballEnabled, enabledByDefault ? 1f : 0f) : (enabledByDefault ? 1f : 0f)) > 0.5f;

        [SerializeField] private Fireball fireballPrefab;

        public string Id => "fireball";
        public bool IsBusy => false;            // instant fire, no busy window
        public bool LocksMovement => false;

        private float nextShotTime;
        private HeroEnemy hero;
        private DifficultyRuntime rt;

        private void Awake()
        {
            hero = GetComponent<HeroEnemy>();
            rt = FindFirstObjectByType<DifficultyRuntime>();
        }

        private void OnEnable()
        {
            // First shot delayed via firstShotDelay so a freshly spawned hero
            // doesn't fire on frame 1 before perception has settled.
            float delay = Eff(Target.HeroFirstShotDelay, hero.Config.firstShotDelay);
            nextShotTime = Time.time + delay;
        }

        public bool IsReady => fireballPrefab != null && hero != null && hero.Grid != null
                               && Time.time >= nextShotTime;

        public float Score(in HeroDecisionContext ctx) => 0.3f;

        public void Begin(in HeroDecisionContext ctx)
        {
            SpawnFireball(ctx);
            float cooldown = Eff(Target.HeroFireballIntervalSeconds, hero.Config.fireballIntervalSeconds);
            nextShotTime = Time.time + cooldown;
        }

        public void Tick(float dt) { }

        public void Cancel()
        {
            // Push the timer forward so a cancelled-then-respawned hero
            // doesn't fire instantly on re-enable.
            nextShotTime = Time.time + Eff(Target.HeroFirstShotDelay, hero.Config.firstShotDelay);
        }

        private void SpawnFireball(in HeroDecisionContext ctx)
        {
            Vector2 fireSize = new Vector2(
                Eff(Target.HeroFireballSizeX, hero.Config.fireballSize.x),
                Eff(Target.HeroFireballSizeY, hero.Config.fireballSize.y));

            var fp = hero.Footprint;
            Vector2 anchor = fp.Anchor;
            BossGrid grid = hero.Grid;
            Vector3 worldPos = grid.FootprintCenterWorld(anchor, fireSize);

            // Aim at ctx.bossCenter, which HeroEnemy fills with the predicted
            // (reaction-lag + extrapolation) position so juking dodges shots.
            Vector2 myCenter = anchor + fp.Footprint * 0.5f;
            Vector2 dir = ctx.bossCenter - myCenter;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector2.left;
            dir.Normalize();

            Fireball instance = Instantiate(fireballPrefab);
            instance.gameObject.SetActive(false);
            instance.transform.position = worldPos;

            GridFootprint instFp = instance.GetComponent<GridFootprint>();
            if (instFp != null) instFp.Configure(anchor, fireSize, grid);

            instance.Direction = dir;
            instance.SetDamage(EffI(Target.HeroFireballDamage, hero.Config.fireballDamage));
            instance.gameObject.SetActive(true);
        }

        private float Eff(Target t, float b) => rt != null ? rt.Get(t, b) : b;
        private int   EffI(Target t, int b)  => rt != null ? rt.GetInt(t, b) : b;
    }
}
