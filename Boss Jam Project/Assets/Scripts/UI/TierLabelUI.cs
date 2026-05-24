using System.Collections;
using BossJam.Difficulty;
using TMPro;
using UnityEngine;
using DifficultyTier = BossJam.Difficulty.Difficulty;

namespace BossJam.UI
{
    /// <summary>
    /// HUD label that mirrors the current difficulty tier. Subscribes to
    /// <see cref="DifficultyRuntime.TierChanged"/> and flashes briefly each
    /// time the tier name actually changes. Persistent between changes so
    /// the player always has the current tier visible.
    /// </summary>
    public class TierLabelUI : MonoBehaviour
    {
        [SerializeField] private DifficultyRuntime runtime;
        [SerializeField] private TMP_Text label;
        [SerializeField] private TMP_Text descriptionLabel;
        [SerializeField] private float flashDuration = 0.4f;
        [SerializeField] private float flashScale = 1.15f;

        private Coroutine flash;

        private void Awake()
        {
            if (runtime == null) runtime = FindFirstObjectByType<DifficultyRuntime>();
            if (runtime == null || label == null)
            {
                Debug.LogWarning($"{nameof(TierLabelUI)}: missing reference; disabling.", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (runtime == null) return;
            runtime.TierChanged += OnTierChanged;
            ApplyEntry(runtime.CurrentTierEntry, flashing: false);
        }

        private void OnDisable()
        {
            if (runtime == null) return;
            runtime.TierChanged -= OnTierChanged;
        }

        private void OnTierChanged(DifficultyTier entry) => ApplyEntry(entry, flashing: true);

        private void ApplyEntry(DifficultyTier entry, bool flashing)
        {
            // Pull the canonical tier name from the runtime so the "Immortal"
            // default (before any debuff lands) surfaces here too.
            label.text = runtime != null ? runtime.CurrentTierName : DifficultyRuntime.ImmortalTierName;
            label.color = entry != null ? entry.tint : Color.white;
            if (descriptionLabel != null)
                descriptionLabel.text = entry != null ? entry.tierDescription : "";
            if (flashing) StartFlash();
        }

        private void StartFlash()
        {
            if (flash != null) StopCoroutine(flash);
            flash = StartCoroutine(FlashRoutine());
        }

        private IEnumerator FlashRoutine()
        {
            var tr = label.transform;
            var baseScale = Vector3.one;
            var t0 = Time.unscaledTime;
            while (Time.unscaledTime - t0 < flashDuration)
            {
                float k = (Time.unscaledTime - t0) / flashDuration;
                float pulse = Mathf.Sin(k * Mathf.PI);
                tr.localScale = baseScale * (1f + (flashScale - 1f) * pulse);
                yield return null;
            }
            tr.localScale = baseScale;
            flash = null;
        }
    }
}
