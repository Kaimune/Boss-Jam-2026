using System;
using System.Collections.Generic;
using UnityEngine;

namespace BossJam.Difficulty
{
    /// <summary>
    /// One tier in the difficulty curve. Authored as an .asset on disk under
    /// Assets/Difficulty/Tiers/. Holds the cosmetic / HUD / narration fields
    /// and an inline polymorphic list of effects that fire when this tier
    /// becomes active.
    ///
    /// Under the reset-on-advance model, EVERY effect this tier should carry
    /// must be listed here — the runtime clears its ledger + flag bag before
    /// applying the new tier, so prior-tier effects do not persist.
    /// </summary>
    [CreateAssetMenu(fileName = "Difficulty", menuName = "BossJam/Difficulty/Tier")]
    public sealed class Difficulty : ScriptableObject
    {
        [Header("HUD / narration")]
        public string tierName;
        [TextArea] public string tierDescription;
        public Color tint = Color.white;
        public Sprite icon;
        [Tooltip("Inline description shown on the intermediate screen.")]
        [TextArea] public string description;
        [Tooltip("Name of a JSON script under Resources/Narration/ (no extension). " +
                 "Empty = no narration; flow goes straight to the difficulty card.")]
        public string narrationScriptName;

        [Header("Effects applied when this tier activates")]
        [Tooltip("Polymorphic inline list. Add concrete effect entries via the + dropdown.")]
        [SerializeReference] public List<IDifficultyEffect> effects = new();
    }
}
