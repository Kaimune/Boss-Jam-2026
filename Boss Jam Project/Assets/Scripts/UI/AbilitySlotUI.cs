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
        [Tooltip("Image with fillMethod=Radial360, origin=Top, clockwise=false. Drains 1→0 over the cooldown.")]
        [SerializeField] private Image iconFill;
        [Tooltip("Centered countdown text — toggled on/off as the cooldown begins and ends.")]
        [SerializeField] private TMP_Text cooldownText;
        [Tooltip("Static key label (e.g. \"J\"). Set once in the inspector; this script doesn't touch it.")]
        [SerializeField] private TMP_Text keyLabel;
        [Tooltip("Optional. If null, the slot resolves the boss via FindFirstObjectByType in Awake.")]
        [SerializeField] private BossController boss;

        private IAttack attack;
        private float castTotal;
        private Coroutine tickCo;

        private void Awake()
        {
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
            if (tickCo != null) { StopCoroutine(tickCo); tickCo = null; }
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
                if (iconFill != null) iconFill.fillAmount = 1f;
                if (tickCo != null) StopCoroutine(tickCo);
                tickCo = StartCoroutine(TickCooldown());
            }
            else if (next == AttackState.Idle)
            {
                if (tickCo != null) { StopCoroutine(tickCo); tickCo = null; }
                ResetVisual();
            }
        }

        private IEnumerator TickCooldown()
        {
            while (attack.TimeToIdle > 0f)
            {
                float r = attack.TimeToIdle;
                if (iconFill != null) iconFill.fillAmount = Mathf.Clamp01(r / castTotal);
                if (cooldownText != null) cooldownText.text = FormatCooldown(r);
                yield return null;
            }
            tickCo = null;
            // Visual cleanup happens via the *→Idle StateChanged callback, which
            // fires the same frame this coroutine exits.
        }

        private void ResetVisual()
        {
            if (iconFill != null) iconFill.fillAmount = 0f;
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
