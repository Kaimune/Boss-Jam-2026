using UnityEngine;

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

        [Header("Visuals (optional)")]
        public GameObject telegraphPrefab;
        public GameObject hitboxPrefab;

        [Header("Animation (reserved; no-op until Animator wired)")]
        public string windupTrigger;
        public string activeTrigger;
        public string recoveryTrigger;
    }
}
