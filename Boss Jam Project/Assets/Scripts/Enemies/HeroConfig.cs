using UnityEngine;

namespace BossJam.Enemies
{
    [CreateAssetMenu(fileName = "HeroConfig", menuName = "BossJam/Hero Config")]
    public class HeroConfig : ScriptableObject
    {
        [Header("HP")]
        [Min(1)] public int maxHp = 3;

        [Header("Tick")]
        [Tooltip("Drives GridMover speed. <1 = faster than tick baseline, >1 = slower.")]
        [Min(0.01f)] public float tickMultiplier = 1f;

        [Header("Kiting")]
        [Tooltip("Distance in cells the hero tries to maintain from the boss.")]
        [Min(0.5f)] public float preferredDistanceCells = 13f;

        [Tooltip("If movement is requested but blocked for this long, flip orbit direction.")]
        [Min(0.05f)] public float stuckFlipSeconds = 0.25f;

        [Header("Reaction")]
        [Tooltip("How long the hero is fooled by a sudden direction change. Under steady motion the prediction cancels this lag out.")]
        [Min(0f)] public float reactionTimeSeconds = 0.25f;

        [Tooltip("Velocity-estimate smoothing window. Bigger = steadier estimate but slower to pick up new motion.")]
        [Min(0.01f)] public float velocityWindowSeconds = 0.1f;

        [Tooltip("Optional cap on extrapolation magnitude (cells). 0 disables. Stops teleports / huge spikes from launching the predicted point off the map.")]
        [Min(0f)] public float maxExtrapolationCells = 0f;

        [Tooltip("How far back perception samples are retained. Should comfortably exceed reaction + velocity window.")]
        [Min(0.1f)] public float perceptionBufferSeconds = 1f;

        [Header("Fireball")]
        [Min(0.1f)] public float fireballIntervalSeconds = 2.5f;
        public Vector2 fireballSize = Vector2.one;

        [Tooltip("Delay before the first shot after spawning.")]
        [Min(0f)] public float firstShotDelay = 1f;
    }
}
