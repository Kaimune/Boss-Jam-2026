using UnityEngine;
using UnityEngine.Serialization;

namespace BossJam.Attacks
{
    [CreateAssetMenu(fileName = "AttackConfig", menuName = "BossJam/Attack Config")]
    public class AttackConfig : ScriptableObject
    {
        [Header("Identity")]
        public string id;
        public AttackHotkey hotkey = AttackHotkey.Primary;

        [Header("Timing (real seconds, multiplied by tickMultiplier at runtime)")]
        [Min(0f)] public float windupSeconds   = 0.15f;
        [Min(0f)] public float activeSeconds   = 0.08f;
        [Min(0f)] public float recoverySeconds = 0.10f;
        [Min(0f)] public float cooldownSeconds = 0.25f;

        [Header("Hit")]
        [Min(0)] public int damage = 1;
        public Vector2 hitboxFootprint = new Vector2(3f, 3f);
        [Tooltip("Cell-space offset from the boss anchor, applied along aim direction.")]
        public float hitboxForwardOffsetCells = 3f;

        [Header("Movement")]
        public bool lockMovementDuringWindup   = true;
        public bool lockMovementDuringActive   = true;
        public bool lockMovementDuringRecovery = false;

        [Header("Charge attack (used by ChargeSlamAttack; ignored by others)")]
        [Min(0.1f)] public float chargeCellsPerSecond = 18f;
        [Tooltip("Extra invulnerability granted after the Active+Recovery window closes. Use this as the 'grace period' so the boss can't be punished the instant the charge ends.")]
        [Min(0f)] public float invulnTrailSeconds = 0.25f;

        [Header("Multi-spawn (used by RockFallAttack; ignored by others)")]
        [Min(1)] public int spawnCount = 12;
        [Tooltip("How long each individual rock's red telegraph shows before impact.")]
        [Min(0.05f)] public float perSpawnTelegraphSeconds = 0.4f;
        [Tooltip("How long each rock's damage hitbox stays alive after impact.")]
        [Min(0.05f)] public float perSpawnHitboxSeconds = 0.25f;
        [Tooltip("Random jitter (± seconds) added to each rock's spawn schedule.")]
        [Min(0f)] public float spawnTimeJitter = 0.15f;
        [Tooltip("Optional 3D rock visual; descends from above during the telegraph window. Leave null for telegraph-only behavior.")]
        public GameObject fallingRockPrefab;
        [Tooltip("World-space height above the impact point at which the falling rock spawns.")]
        [Min(0f)] public float fallStartHeight = 10f;

        [Header("Visuals (optional)")]
        public GameObject telegraphPrefab;
        public GameObject hitboxPrefab;

        [Header("Animation")]
        [Tooltip("Animator state to crossfade to when the attack successfully initiates. " +
                 "Auto-fit speed (on AttackAnimationBinder) stretches/squashes it to the " +
                 "combined Windup+Active+Recovery duration. Leave blank to skip animation.")]
        [FormerlySerializedAs("continuousStateName")]
        public string attackStateName;
    }
}
