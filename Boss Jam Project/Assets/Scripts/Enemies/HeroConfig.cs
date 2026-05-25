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
        [Min(0.5f)] public float preferredDistanceCells = 17f;

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

        [Tooltip("Hero won't fire a fireball when within this distance (cells) of the boss.")]
        [Min(0f)] public float fireballMinDistanceCells = 10f;

        [Header("Dash-Strike")]
        [Tooltip("Damage applied when the dash-strike connects.")]
        [Min(0)] public int dashStrikeDamage = 1;
        [Tooltip("Hit lands when the hero is within this distance (cells) of the boss's footprint EDGE — not the center. 0 means \"touching the boss\". Footprint-aware so the value stays meaningful regardless of how large the boss footprint is.")]
        [Min(0f)] public float dashStrikeMeleeRangeCells = 2f;
        [Tooltip("Kite distance the hero closes to while a punish window is open — the dash launches from here.")]
        [Min(0.5f)] public float dashStrikeTriggerDistanceCells = 5f;
        [Tooltip("Movement-speed multiplier applied during the dash itself.")]
        [Min(1f)] public float dashStrikeSpeedMultiplier = 5.5f;
        [Tooltip("Dash duration. The hero always plays out the full dash; the hit resolves at the END so the strike is unmistakably a dash, not a tap.")]
        [Min(0.05f)] public float dashStrikeDurationSeconds = 0.2f;
        [Tooltip("Max distance (cells, center-to-center) at which the hero will commit to a dash-strike. Beyond this the dash can't plausibly reach, so the hero kites closer first instead of whiffing.")]
        [Min(1f)] public float dashStrikeMaxRangeCells = 12f;
        [Tooltip("Seconds between dash-strikes (independent of the per-window one-hit cap).")]
        [Min(0.05f)] public float dashStrikeCooldownSeconds = 0.9f;

        [Header("Dodge")]
        [Tooltip("Movement-speed multiplier applied during the dodge burst.")]
        [Min(1f)] public float dodgeSpeedMultiplier = 4.5f;
        [Tooltip("How long the dodge boost lasts.")]
        [Min(0.05f)] public float dodgeDurationSeconds = 0.35f;
        [Min(0.05f)] public float dodgeCooldownSeconds = 1.5f;
    }
}
