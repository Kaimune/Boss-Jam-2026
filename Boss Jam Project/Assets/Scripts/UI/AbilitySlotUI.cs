using System.Collections;
using System.Globalization;
using BossJam.Attacks;
using BossJam.Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BossJam.UI
{
    /// <summary>
    /// One ability slot in the cooldown HUD. Subscribes to a single IAttack's
    /// StateChanged event and drives a radial-sweep fill + countdown text only
    /// while that attack is in Cooldown. No Update() loop — the per-frame
    /// coroutine runs only while a cooldown is live.
    /// </summary>
    public sealed class AbilitySlotUI : MonoBehaviour
    {
        [SerializeField] private AttackHotkey hotkey = AttackHotkey.Primary;
        [Tooltip("The ability icon image. Stays fully visible at all times — script never touches its fillAmount or color.")]
        [SerializeField] private Image iconFill;
        [Tooltip("Gray radial overlay rendered on top of the icon. Image with fillMethod=Radial360, origin=Top, clockwise=false. fillAmount sweeps 1→0 over the cast, then flashes white on ready.")]
        [SerializeField] private Image cooldownMask;
        [Tooltip("Centered countdown text — toggled on/off as the cooldown begins and ends.")]
        [SerializeField] private TMP_Text cooldownText;
        [Tooltip("Static key label (e.g. \"J\"). Set once in the inspector; this script doesn't touch it.")]
        [SerializeField] private TMP_Text keyLabel;
        [Tooltip("Optional. If null, the slot resolves the boss via FindFirstObjectByType in Awake.")]
        [SerializeField] private BossController boss;

        [Header("Ready flash")]
        [Tooltip("Color of the white blink shown the moment the ability becomes ready again.")]
        [SerializeField] private Color readyFlashColor = new Color(1f, 1f, 1f, 0.85f);
        [Tooltip("Duration of the ready-blink, in seconds. Set to 0 to disable.")]
        [Min(0f)] [SerializeField] private float readyFlashSeconds = 0.25f;

        private IAttack attack;
        private float castTotal;
        private Coroutine tickCo;
        private Coroutine flashCo;
        private Color cooldownMaskRestColor = new Color(0.1f, 0.1f, 0.1f, 0.5f);

        private void Awake()
        {
            if (cooldownMask != null) cooldownMaskRestColor = cooldownMask.color;
            // Ensure the icon is fully drawn regardless of any stale fillAmount left
            // on the prefab. The script never modifies iconFill after this point.
            if (iconFill != null) iconFill.fillAmount = 1f;

            if (boss == null) boss = FindFirstObjectByType<BossController>();
            if (boss == null)
            {
                Debug.LogWarning($"{nameof(AbilitySlotUI)}: no BossController in scene; disabling slot for {hotkey}.", this);
                enabled = false;
                return;
            }

            var attacks = boss.GetComponentsInChildren<IAttack>(includeInactive: true);
            for (int i = 0; i < attacks.Length; i++)
            {
                var a = attacks[i];
                if (a?.Config != null && a.Config.hotkey == hotkey)
                {
                    attack = a;
                    break;
                }
            }

            if (attack == null)
            {
                Debug.LogWarning($"{nameof(AbilitySlotUI)}: no IAttack found on boss for hotkey {hotkey}; disabling.", this);
                enabled = false;
                return;
            }

            ResetVisual();
        }

        private void OnEnable()
        {
            if (attack == null) return;
            attack.StateChanged += OnAttackStateChanged;
            // Don't try to recover an in-flight cooldown's "total" — the slot
            // visualises cooldowns that begin while it is active. Scene reload
            // resets all state machines anyway, so this only matters around
            // HudVisibility flips, where a half-cooldown wouldn't show right.
            ResetVisual();
        }

        private void OnDisable()
        {
            if (attack == null) return;
            attack.StateChanged -= OnAttackStateChanged;
            StopSlotCoroutines();
            ResetVisual();
        }

        private void OnAttackStateChanged(AttackState prev, AttackState next)
        {
            // Cast begins the moment we leave Idle (TryStart → Windup). League-style:
            // the radial sweep starts draining now and runs until the state machine
            // returns to Idle — Windup + Active + Recovery + Cooldown all included.
            if (prev == AttackState.Idle && next != AttackState.Idle)
            {
                castTotal = Mathf.Max(0.0001f, attack.TimeToIdle);
                if (cooldownText != null) cooldownText.gameObject.SetActive(true);
                if (cooldownMask != null)
                {
                    cooldownMask.color = cooldownMaskRestColor;
                    cooldownMask.fillAmount = 1f;
                }
                StopSlotCoroutines();
                tickCo = StartCoroutine(TickCooldown());
            }
            else if (next == AttackState.Idle)
            {
                StopSlotCoroutines();
                flashCo = StartCoroutine(FlashReady());
            }
        }

        private IEnumerator TickCooldown()
        {
            while (attack.TimeToIdle > 0f)
            {
                float r = attack.TimeToIdle;
                if (cooldownMask != null) cooldownMask.fillAmount = Mathf.Clamp01(r / castTotal);
                if (cooldownText != null) cooldownText.text = FormatCooldown(r);
                yield return null;
            }
            tickCo = null;
            // Visual cleanup happens via the *→Idle StateChanged callback, which
            // fires the same frame this coroutine exits.
        }

        private IEnumerator FlashReady()
        {
            if (cooldownText != null) cooldownText.gameObject.SetActive(false);

            if (cooldownMask == null || readyFlashSeconds <= 0f)
            {
                flashCo = null;
                ResetVisual();
                yield break;
            }

            cooldownMask.fillAmount = 1f;
            float t = 0f;
            while (t < readyFlashSeconds)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / readyFlashSeconds);
                var c = readyFlashColor;
                c.a = Mathf.Lerp(readyFlashColor.a, 0f, k);
                cooldownMask.color = c;
                yield return null;
            }

            flashCo = null;
            ResetVisual();
        }

        private void StopSlotCoroutines()
        {
            if (tickCo != null) { StopCoroutine(tickCo); tickCo = null; }
            if (flashCo != null) { StopCoroutine(flashCo); flashCo = null; }
        }

        private void ResetVisual()
        {
            if (cooldownMask != null)
            {
                cooldownMask.color = cooldownMaskRestColor;
                cooldownMask.fillAmount = 0f;
            }
            if (cooldownText != null) cooldownText.gameObject.SetActive(false);
        }

        /// <summary>
        /// League-style cooldown formatting: integer ceiling for ≥1s,
        /// one decimal for &lt;1s. Pure function — unit-tested.
        /// </summary>
        public static string FormatCooldown(float remaining)
        {
            float r = Mathf.Max(0f, remaining);
            if (r >= 1f)
                return Mathf.CeilToInt(r).ToString(CultureInfo.InvariantCulture);
            return r.ToString("0.0", CultureInfo.InvariantCulture);
        }
    }
}
