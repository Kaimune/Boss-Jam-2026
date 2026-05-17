using BossJam.GridSystem;
using UnityEngine;

namespace BossJam.Enemies
{
    /// <summary>
    /// Stationary placeholder enemy. Blocks boss movement; takes damage from boss hitboxes.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(GridFootprint))]
    public class TrainingDummy : MonoBehaviour, IGridEntity, IDamageable
    {
        [SerializeField, Min(1)] private int hp = 5;
        [SerializeField] private float tickMultiplier = 1f;

        private GridFootprint cachedFootprint;
        public GridFootprint Footprint =>
            cachedFootprint != null ? cachedFootprint : (cachedFootprint = GetComponent<GridFootprint>());
        public float TickMultiplier => tickMultiplier;
        public Team Team => Team.Enemy;

        public Verdict OnEnteredBy(IGridEntity mover)
        {
            if (mover != null && mover.Team == Team.Boss && mover is IDamageDealer dd)
            {
                return Verdict.PassWith(() => TakeDamage(dd.Damage, mover));
            }
            // Boss body or other entities — block (boss must attack to clear).
            return Verdict.Block;
        }

        public void TakeDamage(int amount, IGridEntity source)
        {
            hp -= amount;
            Debug.Log($"TrainingDummy '{name}' took {amount} damage (hp={hp})");
            if (hp <= 0) Destroy(gameObject);
        }
    }
}
