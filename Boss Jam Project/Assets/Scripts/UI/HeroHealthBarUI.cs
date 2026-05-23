using System.Collections.Generic;
using BossJam.Enemies;
using UnityEngine;

namespace BossJam.UI
{
    /// <summary>
    /// HUD bar for the hero. The hero is authored directly in the scene now
    /// (no spawner), so this binds in Start by finding the HeroEnemy in the
    /// scene. Each wave reload rebuilds the scene, so the bind is fresh each
    /// time without any spawn-event plumbing.
    /// </summary>
    public class HeroHealthBarUI : MonoBehaviour
    {
        [SerializeField] private HpSegment segmentPrefab;
        [SerializeField] private Transform segmentRow;

        private HeroEnemy hero;
        private readonly List<HpSegment> segments = new List<HpSegment>();
        private int prevCurrent;

        private void Awake()
        {
            if (segmentPrefab == null || segmentRow == null)
            {
                Debug.LogWarning($"{nameof(HeroHealthBarUI)}: missing reference; disabling.", this);
                enabled = false;
            }
        }

        // Start, not OnEnable: HeroEnemy.Awake must have run so MaxHp is
        // snapshotted before we build segments off it.
        private void Start()
        {
            Bind(FindFirstObjectByType<HeroEnemy>(FindObjectsInactive.Include));
        }

        private void OnDisable() => Unbind();

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
