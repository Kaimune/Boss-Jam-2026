using System;
using System.Collections.Generic;
using BossJam.Configs;
using BossJam.Enemies;
using BossJam.Attacks;
using UnityEngine;

namespace BossJam.Difficulty
{
    /// <summary>
    /// One asset = the entire run's difficulty curve. Holds baseline config
    /// references plus an ordered debuff list applied one-per-hero-kill, with
    /// an optional tier overlay used purely for UI labels.
    ///
    /// Editing this asset (or swapping which one the DifficultyRuntime
    /// references) changes the run's progression without touching any
    /// gameplay script.
    /// </summary>
    [CreateAssetMenu(fileName = "DifficultyProfile", menuName = "BossJam/Difficulty Profile")]
    public class DifficultyProfile : ScriptableObject
    {
        [Header("Baselines (referenced, never mutated)")]
        public GameConfig gameConfig;
        public HeroConfig heroConfig;
        public AttackConfig[] attackConfigs;

        [Header("Tier overlay (UI labels only)")]
        public List<TierBand> tiers = new();

        [Header("Ordered debuffs — applied one per hero kill")]
        public List<DebuffEntry> debuffs = new();
    }

    [Serializable]
    public struct TierBand
    {
        [Tooltip("Display name, e.g. 'Legendary'.")]
        public string name;

        [TextArea] public string description;

        [Tooltip("This tier begins once this many debuffs have been applied.")]
        [Min(0)] public int startsAtDebuffCount;
    }

    /// <summary>
    /// One authored debuff. The <see cref="effect"/> field is polymorphic via
    /// [SerializeReference] — pick a concrete IDebuffEffect from the Inspector
    /// dropdown. The presentation fields (name, description, icon, tint) are
    /// for logging and UI; the effect carries the actual gameplay behavior.
    /// </summary>
    [Serializable]
    public class DebuffEntry
    {
        public string name;
        [TextArea] public string description;
        public Sprite icon;
        public Color tint = Color.white;

        [SerializeReference]
        public IDebuffEffect effect;
    }
}
