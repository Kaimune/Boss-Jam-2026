using UnityEngine;

namespace BossJam.GridSystem
{
    /// <summary>
    /// Mirrors a sibling <see cref="GridFootprint"/> into a sibling
    /// <see cref="Hazard"/> each frame. Use for permanent in-scene actors that
    /// the hero AI should treat as something to avoid stepping into (the
    /// boss body, neutrals, etc.).
    ///
    /// Auto-adds a Hazard if one isn't already on the GameObject. The hazard's
    /// BornAt is set on enable, so the reaction-lag gate on the hero perceives
    /// the boss body almost immediately on scene load.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(GridFootprint))]
    public sealed class GridFootprintHazardSync : MonoBehaviour
    {
        private GridFootprint footprint;
        private Hazard hazard;

        private void Awake()
        {
            footprint = GetComponent<GridFootprint>();
            hazard = GetComponent<Hazard>();
            if (hazard == null) hazard = gameObject.AddComponent<Hazard>();
        }

        // LateUpdate so the footprint's anchor has been written by GridMover
        // / GridFootprint.OnEnable / TryMoveTo this frame.
        private void LateUpdate()
        {
            if (footprint == null || hazard == null) return;
            hazard.Configure(footprint.Anchor, footprint.Footprint);
        }
    }
}
