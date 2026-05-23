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
    /// references plus an ordered debuff list applied one-per-hero-kill.
    /// Each DebuffEntry also carries optional tier metadata used by the HUD.
    ///
    /// Canonical content lives in <see cref="DefaultStages"/>; the SO can be
    /// repopulated from code via the "Reset to Default Stages" context menu.
    /// Designers may still override individual entries in the Inspector.
    /// </summary>
    [CreateAssetMenu(fileName = "DifficultyProfile", menuName = "BossJam/Difficulty Profile")]
    public class DifficultyProfile : ScriptableObject
    {
        [Header("Baselines (referenced, never mutated)")]
        public GameConfig gameConfig;
        public HeroConfig heroConfig;
        public AttackConfig[] attackConfigs;

        [Header("Ordered debuffs — applied one per hero kill")]
        public List<DebuffEntry> debuffs = new();

        [ContextMenu("Reset to Default Stages")]
        private void ResetToDefaults()
        {
#if UNITY_EDITOR
            debuffs = DefaultStages.Build();
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }
    }

    /// <summary>
    /// One authored debuff. The <see cref="effect"/> field is polymorphic via
    /// [SerializeReference] — pick a concrete IDebuffEffect from the Inspector
    /// dropdown. Effect may be null for pure-flavor tier entries that only
    /// update the HUD label without changing gameplay.
    ///
    /// Tier fields: when <see cref="tierName"/> is non-empty and differs from
    /// the runtime's current tier, applying this entry promotes the tier and
    /// raises TierChanged. A blank tierName inherits the previous label.
    /// </summary>
    [Serializable]
    public class DebuffEntry
    {
        public string name;
        [TextArea] public string description;
        public Sprite icon;
        public Color tint = Color.white;

        [Header("Tier (shown on HUD when this entry applies)")]
        public string tierName;
        [TextArea] public string tierDescription;

        [SerializeReference]
        public IDebuffEffect effect;
    }
}
