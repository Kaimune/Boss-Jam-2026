using System.Collections.Generic;
using BossJam.Configs;
using BossJam.Enemies;
using BossJam.Attacks;
using UnityEngine;

namespace BossJam.Difficulty
{
    /// <summary>
    /// One asset = the entire run's difficulty curve. References the baseline
    /// configs and an ordered list of Difficulty tier assets. Applying a tier
    /// is a reset-and-apply operation: each tier defines absolute state.
    /// </summary>
    [CreateAssetMenu(fileName = "DifficultyProfile", menuName = "BossJam/Difficulty Profile")]
    public class DifficultyProfile : ScriptableObject
    {
        [Header("Baselines (referenced, never mutated)")]
        public GameConfig gameConfig;
        public HeroConfig heroConfig;
        public AttackConfig[] attackConfigs;

        [Header("Ordered tiers — applied one per hero kill")]
        public List<Difficulty> tiers = new();
    }
}
