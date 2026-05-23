using UnityEngine;

namespace BossJam.Enemies
{
    [CreateAssetMenu(fileName = "HeroConfig", menuName = "BossJam/Hero Config")]
    public class HeroConfig : ScriptableObject
    {
        [Header("HP")]
        [Min(1)] public int maxHp = 3;

        [Header("Tick")]
        [Tooltip("Drives ITickScalable children (non-movement). <1 = faster than tick baseline, >1 = slower. Hero has no attacks so this currently affects nothing on the hero itself.")]
        [Min(0.01f)] public float tickMultiplier = 1f;

        [Header("Movement")]
        [Tooltip("Baseline movement speed in cells per second.")]
        [Min(0.01f)] public float moveSpeed = 5f;

        [Tooltip("Scalar applied on top of moveSpeed. 1 = baseline, 2 = twice as fast, 0.5 = half speed.")]
        [Min(0.01f)] public float moveSpeedMultiplier = 1f;

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
        [Min(0)] public int fireballDamage = 1;

        [Tooltip("Delay before the first shot after spawning.")]
        [Min(0f)] public float firstShotDelay = 1f;

        [Header("Melee")]
        [Min(0)] public int meleeDamage = 1;
        [Tooltip("Hero is in range to land a melee when distanceToBoss <= this value (cells).")]
        [Min(0.5f)] public float meleeRangeCells = 1.75f;
        [Tooltip("Preferred distance the hero kites at while a boss-attack punish window is open.")]
        [Min(0.5f)] public float meleeApproachDistanceCells = 1.5f;
        [Tooltip("Seconds between melee swings (independent of the per-window one-hit cap).")]
        [Min(0.05f)] public float meleeCooldownSeconds = 0.9f;

        [Header("Dodge")]
        [Tooltip("Movement-speed multiplier applied during the dodge burst.")]
        [Min(1f)] public float dodgeSpeedMultiplier = 4.5f;
        [Tooltip("How long the dodge boost lasts.")]
        [Min(0.05f)] public float dodgeDurationSeconds = 0.35f;
        [Min(0.05f)] public float dodgeCooldownSeconds = 1.5f;
    }
}
