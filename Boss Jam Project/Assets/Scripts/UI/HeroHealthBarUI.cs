using System.Collections.Generic;
using BossJam.Enemies;
using UnityEngine;

namespace BossJam.UI
{
    public class HeroHealthBarUI : MonoBehaviour
    {
        [SerializeField] private HeroEnemy hero;
        [SerializeField] private HpSegment segmentPrefab;
        [SerializeField] private Transform segmentRow;

        private readonly List<HpSegment> segments = new List<HpSegment>();
        private int prevCurrent;
        private bool built;

        private void Awake()
        {
            if (hero == null || segmentPrefab == null || segmentRow == null)
            {
                Debug.LogWarning($"{nameof(HeroHealthBarUI)}: missing reference; disabling.", this);
                enabled = false;
            }
        }

        private void Start()
        {
            Build(hero.MaxHp);
            SyncFromHero();
        }

        private void OnEnable()
        {
            if (hero == null) return;
            hero.HpChanged += OnHpChanged;
            if (built) SyncFromHero();
        }

        private void OnDisable()
        {
            if (hero == null) return;
            hero.HpChanged -= OnHpChanged;
        }

        private void Build(int max)
        {
            for (int i = 0; i < max; i++)
            {
                var seg = Instantiate(segmentPrefab, segmentRow);
                seg.SetFilled(true);
                segments.Add(seg);
            }
            built = true;
        }

        private void SyncFromHero()
        {
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
