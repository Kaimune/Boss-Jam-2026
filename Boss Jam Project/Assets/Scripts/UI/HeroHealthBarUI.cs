using System.Collections.Generic;
using BossJam.Enemies;
using BossJam.Game;
using UnityEngine;

namespace BossJam.UI
{
    /// <summary>
    /// HUD bar for the hero. Heroes are spawned dynamically by the
    /// <see cref="HeroSpawner"/>, so the bar can't hold a stable serialized
    /// reference — it subscribes to <c>HeroSpawner.HeroSpawned</c> and rebinds
    /// on every spawn, rebuilding the segment row to match the new hero's
    /// MaxHp (which can change across tiers if a debuff scales HeroMaxHp).
    /// </summary>
    public class HeroHealthBarUI : MonoBehaviour
    {
        [SerializeField] private HeroSpawner spawner;
        [SerializeField] private HpSegment segmentPrefab;
        [SerializeField] private Transform segmentRow;

        private HeroEnemy hero;
        private readonly List<HpSegment> segments = new List<HpSegment>();
        private int prevCurrent;

        private void Awake()
        {
            if (spawner == null) spawner = FindFirstObjectByType<HeroSpawner>();
            if (spawner == null || segmentPrefab == null || segmentRow == null)
            {
                Debug.LogWarning($"{nameof(HeroHealthBarUI)}: missing reference; disabling.", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (spawner == null) return;
            spawner.HeroSpawned += OnHeroSpawned;
            // Bind to whoever is already alive when the bar wakes up — covers
            // the case where the spawner spawned a hero before this OnEnable.
            if (spawner.CurrentHero != null) Bind(spawner.CurrentHero);
        }

        private void OnDisable()
        {
            if (spawner != null) spawner.HeroSpawned -= OnHeroSpawned;
            Unbind();
        }

        private void OnHeroSpawned(HeroEnemy next) => Bind(next);

        private void Bind(HeroEnemy next)
        {
            Unbind();
            hero = next;
            if (hero == null) return;
            hero.HpChanged += OnHpChanged;
            BuildSegments(hero.MaxHp);
            SyncFromHero();
        }

        private void Unbind()
        {
            if (hero != null) hero.HpChanged -= OnHpChanged;
            hero = null;
        }

        private void BuildSegments(int max)
        {
            for (int i = segmentRow.childCount - 1; i >= 0; i--)
                Destroy(segmentRow.GetChild(i).gameObject);
            segments.Clear();
            for (int i = 0; i < max; i++)
            {
                var seg = Instantiate(segmentPrefab, segmentRow);
                seg.SetFilled(true);
                segments.Add(seg);
            }
        }

        private void SyncFromHero()
        {
            if (hero == null) return;
            int cur = hero.CurrentHp;
            for (int i = 0; i < segments.Count; i++)
                segments[i].SetFilled(i < cur);
            prevCurrent = cur;
        }

        private void OnHpChanged(int current, int max)
        {
            for (int i = 0; i < segments.Count; i++)
                segments[i].SetFilled(i < current);
            for (int i = current; i < prevCurrent && i < segments.Count; i++)
                segments[i].Flash();
            prevCurrent = current;
        }
    }
}
