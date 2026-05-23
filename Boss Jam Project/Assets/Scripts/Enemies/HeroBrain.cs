using System.Collections.Generic;
using UnityEngine;

namespace BossJam.Enemies
{
    /// <summary>
    /// Picks one ability per frame for the hero AI. Lives as a sibling
    /// component on the hero. HeroEnemy registers abilities into it at
    /// Awake based on the wave-index loadout, then queries Choose() each
    /// Update to know what to fire.
    ///
    /// Also tracks the "one melee per boss punish window" rule: once melee
    /// fires while the boss is in Recovery/Cooldown, the brain forbids
    /// further melee until the window closes and a new one opens.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HeroBrain : MonoBehaviour
    {
        private readonly List<IHeroAbility> abilities = new();
        private IHeroAbility activeAbility;
        private bool punishWindowConsumed;
        private bool prevWindowOpen;

        // HeroEnemy reads this to drive the dynamic preferred-kite-distance:
        // when a window is open AND we haven't already spent our hit, hero
        // approaches; otherwise hero kites at default distance.
        public bool PunishWindowConsumed => punishWindowConsumed;

        public void RegisterAbility(IHeroAbility ability)
        {
            if (ability != null && !abilities.Contains(ability))
                abilities.Add(ability);
        }

        /// <summary>
        /// Returns the ability to use this frame, or null if the brain wants
        /// the hero to keep doing what it was doing (kite, idle, etc.).
        /// Should be called once per Update with a fresh context.
        /// </summary>
        public IHeroAbility Choose(in HeroDecisionContext ctx)
        {
            // Rising edge tracking for the punish-window consumption flag.
            // When the window closes (true → false), reset so the next window
            // is fresh. When it opens, leave the flag where it is (false).
            if (prevWindowOpen && !ctx.bossInPunishWindow) punishWindowConsumed = false;
            prevWindowOpen = ctx.bossInPunishWindow;

            if (activeAbility != null && activeAbility.IsBusy) return null;
            activeAbility = null;

            IHeroAbility best = null;
            float bestScore = 0f;
            for (int i = 0; i < abilities.Count; i++)
            {
                var a = abilities[i];
                if (!a.IsReady) continue;
                // Once-per-window cap: melee is gated out for the rest of this
                // window after the first swing. Other abilities are not.
                if (a is HeroMelee && punishWindowConsumed) continue;

                float s = a.Score(ctx);
                if (s > bestScore) { best = a; bestScore = s; }
            }
            return best;
        }

        /// <summary>
        /// Commit to an ability picked by Choose. Drives its Begin() and
        /// marks the brain as actively running it. Call once per pick.
        /// </summary>
        public void Commit(IHeroAbility ability, in HeroDecisionContext ctx)
        {
            if (ability == null) return;
            ability.Begin(ctx);
            activeAbility = ability;
            // Melee is the only ability that spends the punish window in Spec 1.
            if (ability is HeroMelee && ctx.bossInPunishWindow)
                punishWindowConsumed = true;
        }

        public void TickAll(float dt)
        {
            for (int i = 0; i < abilities.Count; i++)
                abilities[i]?.Tick(dt);
        }

        public void CancelAll()
        {
            for (int i = 0; i < abilities.Count; i++)
                abilities[i]?.Cancel();
            activeAbility = null;
        }
    }
}
