namespace BossJam.GridSystem
{
    public interface IDamageable
    {
        void TakeDamage(int amount, IGridEntity source);
    }
}
