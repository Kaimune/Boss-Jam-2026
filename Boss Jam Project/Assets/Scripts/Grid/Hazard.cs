using System.Collections.Generic;
using UnityEngine;

namespace BossJam.GridSystem
{
    /// <summary>
    /// Lightweight marker for "this cell-space rect is dangerous to step into".
    /// Attack telegraphs and hitboxes (rock fall, ground slash, charge slam,
    /// boss body) attach a Hazard so the hero AI can avoid them without each
    /// attack having to know about the hero.
    ///
    /// Separate from <see cref="GridFootprint"/>: GridFootprint registers
    /// cells with the collision system and gates movement. Hazard is purely
    /// perceptual — it tells AI "don't pick this cell as a steering target".
    /// Hazards never block movement on their own.
    ///
    /// Spawners call Configure(anchor, footprint) right after Instantiate to
    /// place the rect; for telegraphs/hitboxes that follow the boss across
    /// frames, call Configure again on each update tick.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Hazard : MonoBehaviour
    {
        public Vector2 Anchor    { get; private set; }
        public Vector2 Footprint { get; private set; } = Vector2.one;

        // Time.time at which this hazard became visible. The hero applies its
        // reaction-time gate against this so freshly-spawned telegraphs aren't
        // dodged instantly (the hero hasn't perceived them yet).
        public float BornAt { get; private set; }

        // All currently-enabled hazards in the scene. Maintained by
        // OnEnable / OnDisable so the hero can iterate it once per frame
        // without a FindObjectsByType scan.
        private static readonly List<Hazard> _all = new();
        public static IReadOnlyList<Hazard> All => _all;

        public void Configure(Vector2 anchor, Vector2 footprint)
        {
            Anchor = anchor;
            Footprint = footprint;
        }

        private void OnEnable()
        {
            BornAt = Time.time;
            if (!_all.Contains(this)) _all.Add(this);
        }

        private void OnDisable()
        {
            _all.Remove(this);
        }
    }
}
