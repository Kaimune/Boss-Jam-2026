namespace BossJam.Player
{
    /// <summary>
    /// Anything that can be temporarily un-hittable. Implemented by actors
    /// that have an iframe window — boss (charge slam) + hero (stun after
    /// hit). Consumed by visual feedback components like IframeBlinker so
    /// they don't have to know which actor they're attached to.
    /// </summary>
    public interface IInvulnerable
    {
        bool IsInvulnerable { get; }
    }
}
