namespace BossJam.Difficulty
{
    /// <summary>
    /// Controls extra respawn behaviour on hero damage/death. Set by
    /// HeroRespawnEffect when a tier with respawn rules lands; consulted by
    /// HeroEnemy before lethal damage resolves.
    /// </summary>
    public enum HeroRespawnMode
    {
        None,
        SaveScumOnFirstOneHp,
        FullHpIfNotInstakilled,
    }
}
