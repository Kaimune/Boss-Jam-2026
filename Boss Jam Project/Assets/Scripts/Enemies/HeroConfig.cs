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
        [Tooltip("Movement-speed multiplier applied during the dash. Combined with dashStrikeDurationSeconds this defines max dash reach — the score gate uses that reach (no separate \"max range\" knob) so the hero only commits dashes that can actually land.")]
        [Min(1f)] public float dashStrikeSpeedMultiplier = 5.5f;
        [Tooltip("Maximum dash duration. The dash uses a fixed speed (dashStrikeSpeedMultiplier × move speed) but its duration scales down to end exactly at the boss edge — capped here so it can never run for longer than this even if the target is unreachable.")]
        [Min(0.05f)] public float dashStrikeDurationSeconds = 0.2f;
        [Tooltip("Minimum dash duration. Keeps close-range dashes visible (so they don't look like an instant tap).")]
        [Min(0.01f)] public float dashStrikeMinDurationSeconds = 0.08f;
        [Tooltip("Seconds between dash-strikes (independent of the per-window one-hit cap).")]
        [Min(0.05f)] public float dashStrikeCooldownSeconds = 0.9f;
        [Tooltip("Initial cooldown on both dash charges at hero spawn — keeps the hero from insta-attacking on scene load.")]
        [Min(0f)] public float dashStrikeSpawnDelaySeconds = 1.5f;
        [Tooltip("After a dash-strike lands a hit on the boss, the hero is locked out of all offensive abilities (dash-strike + fireball) for this many seconds. Gives the player breathing room between hits.")]
        [Min(0f)] public float dashStrikePostHitLockoutSeconds = 2f;
        [Tooltip("After a dash-strike ends WITHOUT landing a hit (whiff or invuln target), the hero is still locked out of offensive abilities for this many seconds. Always arms; the post-hit lockout above wins when both apply (lockout only ever extends).")]
        [Min(0f)] public float dashStrikePostMissLockoutSeconds = 1f;

        [Header("Dodge")]
        [Tooltip("Movement-speed multiplier applied during the dodge burst.")]
        [Min(1f)] public float dodgeSpeedMultiplier = 4.5f;
        [Tooltip("How long the dodge boost lasts.")]
        [Min(0.05f)] public float dodgeDurationSeconds = 0.35f;
        [Min(0.05f)] public float dodgeCooldownSeconds = 1.5f;
    }
}
