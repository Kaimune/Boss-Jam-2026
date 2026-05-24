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
    /// StateChanged event and drives a radial-sweep mask + countdown text only
    /// while that attack is in Cooldown. No Update() loop — the per-frame
    /// coroutine runs only while a cooldown is live.
    /// </summary>
    public sealed class AbilitySlotUI : MonoBehaviour
    {
        [SerializeField] private AttackHotkey hotkey = AttackHotkey.Primary;
        [Tooltip("The ability icon image. Always visible; the script does not touch it.")]
        [SerializeField] private Image iconFill;
        [Tooltip("Radial overlay rendered on top of the icon. Becomes a white circle at fillAmount=1 when a cast begins, drains radially to 0 over the cooldown, then briefly blinks before vanishing.")]
        [SerializeField] private Image cooldownMask;
        [Tooltip("Centered countdown text — toggled on/off as the cooldown begins and ends.")]
        [SerializeField] private TMP_Text cooldownText;
        [Tooltip("Static key label (e.g. \"J\"). Set once in the inspector; this script doesn't touch it.")]
        [SerializeField] private TMP_Text keyLabel;
        [Tooltip("Optional. If null, the slot resolves the boss via FindFirstObjectByType in Awake.")]
        [SerializeField] private BossController boss;

        [Header("Cooldown circle")]
        [Tooltip("Resting color of the cooldown circle while it drains.")]
        [SerializeField] private Color cooldownColor = new Color(1f, 1f, 1f, 0.3f);

        [Header("Ready blink")]
        [Tooltip("Color the circle flashes to when the ability becomes ready again.")]
        [SerializeField] private Color blinkColor = new Color(1f, 1f, 1f, 1f);
        [Tooltip("Blink duration, seconds. Set to 0 to disable.")]
        [Min(0f)] [SerializeField] private float blinkSeconds = 0.2f;

        private IAttack attack;
        private float castTotal;
        private Coroutine tickCo;
        private Coroutine blinkCo;

        // Shared procedurally-generated circle sprite. Filled at runtime so the
        // mask can render as a clean radial pie wedge without depending on a
        // project-side circle asset. Antialiased edge so the radial sweep is
        // smooth at any size.
        private static Sprite circleSprite;

        private void Awake()
        {
            if (cooldownMask != null && cooldownMask.sprite == null)
            {
                cooldownMask.sprite = GetCircleSprite();
            }

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
            if (prev == AttackState.Idle && next != AttackState.Idle)
            {
                castTotal = Mathf.Max(0.0001f, attack.TimeToIdle);
                if (cooldownText != null) cooldownText.gameObject.SetActive(true);
                if (cooldownMask != null)
                {
                    cooldownMask.color = cooldownColor;
                    cooldownMask.fillAmount = 1f;
                }
                StopSlotCoroutines();
                tickCo = StartCoroutine(TickCooldown());
            }
            else if (next == AttackState.Idle)
            {
                StopSlotCoroutines();
                if (cooldownText != null) cooldownText.gameObject.SetActive(false);
                if (blinkSeconds > 0f && cooldownMask != null)
                {
                    blinkCo = StartCoroutine(Blink());
                }
                else
                {
                    ResetVisual();
                }
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
        }

        private IEnumerator Blink()
        {
            cooldownMask.fillAmount = 1f;
            float t = 0f;
            while (t < blinkSeconds)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / blinkSeconds);
                var c = blinkColor;
                c.a = Mathf.Lerp(blinkColor.a, 0f, k);
                cooldownMask.color = c;
                yield return null;
            }
            blinkCo = null;
            ResetVisual();
        }

        private void StopSlotCoroutines()
        {
            if (tickCo != null) { StopCoroutine(tickCo); tickCo = null; }
            if (blinkCo != null) { StopCoroutine(blinkCo); blinkCo = null; }
        }

        private void ResetVisual()
        {
            if (cooldownMask != null)
            {
                cooldownMask.color = cooldownColor;
                cooldownMask.fillAmount = 0f;
            }
            if (cooldownText != null) cooldownText.gameObject.SetActive(false);
        }

        private static Sprite GetCircleSprite()
        {
            if (circleSprite != null) return circleSprite;
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color32[size * size];
            float radius = size * 0.5f;
            float cx = radius - 0.5f;
            float cy = radius - 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - cx;
                    float dy = y - cy;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(radius - d);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(updateMipmaps: false, makeNoLongerReadable: true);
            circleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            circleSprite.name = "AbilitySlotCircle";
            return circleSprite;
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
