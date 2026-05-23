using System.Collections.Generic;
using UnityEngine;

namespace BossJam.Difficulty
{
    /// <summary>
    /// Canonical difficulty curve. Edit this file to change the default
    /// progression, then right-click DifficultyProfile.asset →
    /// "Reset to Default Stages" to push these entries into the SO.
    ///
    /// Effects are authored as separate ScriptableObject assets (see
    /// <see cref="DebuffEffect"/>) and wired onto each entry's `effects` list
    /// in the inspector. This file only seeds the textual / cosmetic fields
    /// (name, description, tier name + flavour, tint) — designers fill in
    /// effects per entry after the reset.
    /// </summary>
    public static class DefaultStages
    {
        public static List<DebuffEntry> Build() => new()
        {
            new DebuffEntry
            {
                name = "Quickening",
                description = "Boss acts 15% faster.",
                tierName = "Tier 1 Awakened",
                tierDescription = "The boss begins to move with purpose.",
                tint = new Color(1f, 0.85f, 0.5f),
            },
            new DebuffEntry
            {
                name = "Hardened",
                description = "Boss gains +2 max HP.",
                tierName = "Tier 2 Hardened",
                tierDescription = "Its hide thickens.",
                tint = new Color(0.7f, 0.9f, 1f),
            },
            new DebuffEntry
            {
                name = "Frenzy",
                description = "Boss acts 25% faster and gains +3 HP.",
                tierName = "Tier 3 Frenzied",
                tierDescription = "Pain becomes fuel.",
                tint = new Color(1f, 0.5f, 0.5f),
            },
        };
    }
}
