using System;
using System.Collections;
using BossJam.Enemies;
using BossJam.GridSystem;
using UnityEngine;

namespace BossJam.Cutscene
{
    /// <summary>
    /// Walks the AI hero from an off-grid point west of the arena to a fight-
    /// start cell on the grid. One step per BossGrid.TickDuration, lerping
    /// between cell centres. On arrival, registers the hero with the grid at
    /// the fight-start cell and raises IntroComplete. Esc skips to the end
    /// via SnapToEnd().
    /// </summary>
    public sealed class IntroDirector : MonoBehaviour
    {
        [SerializeField] private Transform heroIntroSpawn;
        [SerializeField] private Transform heroFightStart;
        [SerializeField] private BossGrid grid;

        public event Action IntroComplete;
        public Transform HeroIntroSpawn => heroIntroSpawn;
        public Transform HeroFightStart => heroFightStart;

        private Coroutine walkRoutine;
        private HeroEnemy hero;
        private GridFootprint footprint;

        private void Awake()
        {
            if (grid == null) grid = FindFirstObjectByType<BossGrid>();
        }

        public void Begin(HeroEnemy heroInstance)
        {
            if (heroInstance == null)
            {
                Debug.LogError($"{nameof(IntroDirector)}: Begin called with null hero.");
                Complete();
                return;
            }
            hero = heroInstance;
            footprint = hero.GetComponent<GridFootprint>();

            // Park the hero off-grid + disable grid registration during walk.
            if (footprint != null) footprint.enabled = false;
            ToggleCombatComponents(false);
            if (heroIntroSpawn != null) hero.transform.position = heroIntroSpawn.position;

            walkRoutine = StartCoroutine(WalkIn());
        }

        public void SnapToEnd()
        {
            if (walkRoutine != null) { StopCoroutine(walkRoutine); walkRoutine = null; }
            Complete();
        }

        private IEnumerator WalkIn()
        {
            if (hero == null || grid == null || heroFightStart == null) { Complete(); yield break; }

            float cellSize = grid.CellSize;
            float tick = grid.TickDuration;
            if (tick <= 0.001f) tick = 0.2f;

            while (true)
            {
                Vector3 here = hero.transform.position;
                Vector3 target = heroFightStart.position;
                float dx = target.x - here.x;
                if (Mathf.Abs(dx) < cellSize * 0.5f) break;

                Vector3 stepEnd = here + new Vector3(Mathf.Sign(dx) * cellSize, 0f, 0f);
                float t = 0f;
                while (t < tick)
                {
                    t += Time.deltaTime;
                    hero.transform.position = Vector3.Lerp(here, stepEnd, Mathf.Clamp01(t / tick));
                    yield return null;
                }
                hero.transform.position = stepEnd;
            }
            Complete();
        }

        private void Complete()
        {
            if (hero != null && grid != null && heroFightStart != null)
            {
                hero.transform.position = heroFightStart.position;
                if (footprint != null)
                {
                    var cell = grid.WorldToCell(heroFightStart.position);
                    footprint.Configure(new Vector2(cell.x, cell.y), footprint.Footprint, grid);
                    footprint.enabled = true;
                }
                ToggleCombatComponents(true);
            }
            IntroComplete?.Invoke();
        }

        private void ToggleCombatComponents(bool enabledFlag)
        {
            if (hero == null) return;
            // HeroKiteSteering is a static utility (not a component) — nothing to toggle there.
            // HeroEnemy.Update already early-returns when state != Playing, so the static
            // helper isn't called during the cutscene anyway.
            ToggleByTypeName(hero.gameObject, "BossJam.Enemies.FireballSpawner", enabledFlag);
        }

        private static void ToggleByTypeName(GameObject go, string fullName, bool enabledFlag)
        {
            var t = System.Type.GetType(fullName + ", Assembly-CSharp");
            if (t == null) return;
            // Skip non-component types — guards against static utility classes living
            // in the same assembly accidentally matching by name.
            if (!typeof(Component).IsAssignableFrom(t)) return;
            var c = go.GetComponent(t) as Behaviour;
            if (c != null) c.enabled = enabledFlag;
        }
    }
}
