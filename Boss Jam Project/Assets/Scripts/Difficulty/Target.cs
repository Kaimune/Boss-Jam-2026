namespace BossJam.Difficulty
{
    /// <summary>
    /// Stat surface for the difficulty modifier system. Each entry names one
    /// queryable stat. Consumers query the runtime for an effective value
    /// against their baseline; modifiers attached to the matching Target
    /// participate in the fold.
    ///
    /// For attack-specific fields that don't deserve a global enum entry,
    /// use <see cref="AttackExtension"/> along with a string key passed in
    /// the query and stored on the modifier entry. That avoids enum bloat
    /// when each new attack adds 2–3 unique stats.
    /// </summary>
    public enum Target
    {
        // ── Hero ─────────────────────────────────────────────
        HeroMaxHp,
        HeroTickMultiplier,
        HeroPreferredDistanceCells,
        HeroStuckFlipSeconds,
        HeroReactionTimeSeconds,
        HeroVelocityWindowSeconds,
        HeroFireballIntervalSeconds,
        HeroFireballSizeX,
        HeroFireballSizeY,
        HeroFirstShotDelay,
        HeroFireballCooldownSeconds,
        HeroFireballDamage,

        // ── Hero — Melee ─────────────────────────────────────
        HeroMeleeEnabled,
        HeroMeleeDamage,
        HeroMeleeRangeCells,
        HeroMeleeApproachDistanceCells,
        HeroMeleeCooldownSeconds,

        // ── Hero — Dodge ─────────────────────────────────────
        HeroDodgeEnabled,
        HeroDodgeSpeedMultiplier,
        HeroDodgeDurationSeconds,
        HeroDodgeCooldownSeconds,

        // ── Hero — Fireball enable toggle ────────────────────
        HeroFireballEnabled,

        // ── Boss ─────────────────────────────────────────────
        BossMaxHp,
        BossTickMultiplier,

        // ── Attack — common (applies to any AttackConfig.id) ─
        BossAttackWindupSeconds,
        BossAttackActiveSeconds,
        BossAttackRecoverySeconds,
        BossAttackCooldownSeconds,
        BossAttackDamage,
        BossAttackHitboxForwardOffsetCells,
        BossAttackHitboxFootprintX,
        BossAttackHitboxFootprintY,

        // ── Attack — per-attack extension (string key disambiguates) ─
        AttackExtension,
    }

    public enum Op
    {
        Mul,
        Add,
        Override,
    }
}
