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

        [Header("Timing")]
        [Min(0f)] public float windupSeconds = 0.15f;
        [Min(0f)] public float activeSeconds = 0.08f;
        [Min(0f)] public float recoverySeconds = 0.10f;
        [Min(0f)] public float cooldownSeconds = 0.25f;

        [Header("Hit")]
        [Min(0)] public int damage = 1;
        public Vector2 hitboxFootprint = new Vector2(3f, 3f);
        public float hitboxForwardOffsetCells = 3f;

        [Header("Movement")]
        public bool lockMovementDuringWindup = true;
        public bool lockMovementDuringActive = true;
        public bool lockMovementDuringRecovery = false;

        [Header("Charge attack")]
        [Min(0.1f)] public float chargeCellsPerSecond = 18f;
        [Min(0f)] public float invulnTrailSeconds = 0.25f;

        [Header("Multi-spawn")]
        [Min(1)] public int spawnCount = 12;
        [Min(0.05f)] public float perSpawnTelegraphSeconds = 0.4f;
        [Min(0.05f)] public float perSpawnHitboxSeconds = 0.25f;
        [Min(0f)] public float spawnTimeJitter = 0.15f;

        [Tooltip("Put your 3 rock prefabs here.")]
        public GameObject[] fallingRockPrefabs;

        [Min(0f)] public float fallStartHeight = 10f;

        [Header("Visuals")]
        public GameObject telegraphPrefab;
        public GameObject hitboxPrefab;

        [Header("Animation")]
        [FormerlySerializedAs("continuousStateName")]
        public string attackStateName;
    }
}