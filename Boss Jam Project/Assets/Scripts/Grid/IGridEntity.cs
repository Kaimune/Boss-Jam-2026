namespace BossJam.GridSystem
{
    public interface IGridEntity
    {
        GridFootprint Footprint { get; }
        float TickMultiplier { get; }
        Team Team { get; }
        Verdict OnEnteredBy(IGridEntity mover);
    }
}