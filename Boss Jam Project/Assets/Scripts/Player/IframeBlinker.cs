using UnityEngine;

namespace BossJam.Player
{
    /// <summary>
    /// Visual blink for an actor while it has invulnerability frames. Toggles
    /// the enabled flag on every Renderer under <see cref="rendererRoot"/> at
    /// <see cref="blinkHz"/> while the host's <see cref="IInvulnerable"/>
    /// source reports invulnerability; clamps back to fully visible the
    /// moment iframes lapse.
    ///
    /// Source is resolved via GetComponent on Awake — drop this on any
    /// GameObject whose own component implements IInvulnerable (BossController,
    /// HeroEnemy, …) and the blinker auto-binds.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class IframeBlinker : MonoBehaviour
    {
        [Tooltip("Root whose child Renderers are toggled. Defaults to this transform if left null.")]
        [SerializeField] private Transform rendererRoot;
        [Tooltip("Blink cycles per second while iframes are active. 10 reads as a fast flicker.")]
        [SerializeField, Min(0.5f)] private float blinkHz = 10f;

        private IInvulnerable source;
        private Renderer[] renderers;
        private bool wasInvuln;

        private void Awake()
        {
            source = GetComponent<IInvulnerable>();
            if (rendererRoot == null) rendererRoot = transform;
            RebuildRenderers();
        }

        private void RebuildRenderers()
        {
            renderers = rendererRoot != null
                ? rendererRoot.GetComponentsInChildren<Renderer>(includeInactive: true)
                : System.Array.Empty<Renderer>();
        }

        private void LateUpdate()
        {
            bool invuln = source != null && source.IsInvulnerable;

            if (!invuln)
            {
                // Restore visibility once iframes end. Cheap belt-and-suspenders:
                // skip the per-renderer write when we already cleared last frame.
                if (wasInvuln) SetAll(true);
                wasInvuln = false;
                return;
            }

            wasInvuln = true;
            // Square wave at blinkHz — visible on even half-cycles, hidden on odd.
            float period = 1f / Mathf.Max(0.5f, blinkHz);
            bool visible = Mathf.FloorToInt(Time.unscaledTime / (period * 0.5f)) % 2 == 0;
            SetAll(visible);
        }

        private void SetAll(bool enabled)
        {
            if (renderers == null) return;
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null) renderers[i].enabled = enabled;
        }
    }
}
