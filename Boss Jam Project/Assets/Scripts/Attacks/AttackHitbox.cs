using BossJam.GridSystem;
using UnityEngine;

namespace BossJam.Attacks
{
    /// <summary>
    /// Stationary damage emitter. Spawned by a BossAttack at its OnEnterActive,
    /// registered on the grid via its GridFootprint, then destroyed by the host
    /// at OnEnterRecovery. While alive, any enemy whose cell it overlaps takes
    /// damage once on entry (Verdict.PassWith fires once per overlap).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(GridFootprint))]
    public class AttackHitbox : MonoBehaviour, IGridEntity, ITickScalable, IDamageDealer
    {
        [SerializeField, Min(0)] private int damage = 1;
        public int Damage => damage;
        public void SetDamage(int d) => damage = d;

        private GridFootprint cachedFootprint;
        public GridFootprint Footprint =>
            cachedFootprint != null ? cachedFootprint : (cachedFootprint = GetComponent<GridFootprint>());
        public float TickMultiplier => tickMultiplier;
        public Team Team => Team.Boss;

        // We never react to others entering us — enemies decide what to do with us via their OnEnteredBy.
        public Verdict OnEnteredBy(IGridEntity mover) => Verdict.Pass;

        private float tickMultiplier = 1f;
        public void ApplyTick(float m) => tickMultiplier = m;

        private void Awake()
        {
            var rt = FindFirstObjectByType<BossJam.Difficulty.DifficultyRuntime>();
            if (rt != null && rt.State != null && !rt.State.showDebugVisuals)
            {
                foreach (var mr in GetComponentsInChildren<MeshRenderer>(true))
                    mr.enabled = false;
            }
        }
    }
}
