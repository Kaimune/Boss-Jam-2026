using System.Collections.Generic;
using BossJam.Player;
using UnityEngine;

namespace BossJam.UI
{
    public class BossHealthBarUI : MonoBehaviour
    {
        [SerializeField] private BossController boss;
        [SerializeField] private HpSegment segmentPrefab;
        [SerializeField] private Transform segmentRow;

        private readonly List<HpSegment> segments = new List<HpSegment>();
        private int prevCurrent;
        private bool built;

        private void Awake()
        {
            if (boss == null || segmentPrefab == null || segmentRow == null)
            {
                Debug.LogWarning($"{nameof(BossHealthBarUI)}: missing reference; disabling.", this);
                enabled = false;
            }
        }

        private void Start()
        {
            Build(boss.MaxHp);
            SyncFromBoss();
        }

        private void OnEnable()
        {
            if (boss == null) return;
            boss.HpChanged += OnHpChanged;
            if (built) SyncFromBoss();
        }

        private void OnDisable()
        {
            if (boss == null) return;
            boss.HpChanged -= OnHpChanged;
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

        private void SyncFromBoss()
        {
            int cur = boss.CurrentHp;
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
