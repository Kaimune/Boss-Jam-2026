namespace BossJam.GridSystem
{
    public interface IGridEntity
    {
        GridFootprint Footprint {get; }
        float TickMultiplier { get; }
    }
}