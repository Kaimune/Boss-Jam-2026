using UnityEngine;

namespace BossJam.Enemies
{
    /// <summary>
    /// One thing the hero AI can choose to do this frame. Each ability is a
    /// MonoBehaviour sibling on the hero. The brain queries Score() to pick
    /// the best option, then calls Begin() to commit. Composes (privately)
    /// an AttackStateMachine for cooldown bookkeeping where appropriate.
    /// </summary>
    public interface IHeroAbility
    {
        string Id { get; }

        // True between Begin() and the end of the ability's active phase.
        bool IsBusy { get; }

        // True when this ability fully suppresses kite-steering input. Dodge
        // does NOT lock movement — it accelerates whatever direction kiting
        // is already producing.
        bool LocksMovement { get; }

        // Cooldown elapsed + any local preconditions satisfied (e.g. in range).
        bool IsReady { get; }

        // Utility weight for this frame. 0 = inert. Higher = more wanted.
        // The brain picks the highest-scoring ready ability.
        float Score(in HeroDecisionContext ctx);

        // Commit to this ability — fire effect, start cooldown, etc.
        void Begin(in HeroDecisionContext ctx);

        // Per-frame internal tick (state machine progression, etc.).
        void Tick(float dt);

        // Hard reset, e.g. on hero death.
        void Cancel();
    }

    /// <summary>
    /// Per-frame snapshot of everything the hero brain + abilities need to
    /// decide what to do. Built fresh each Update by HeroEnemy.
    /// </summary>
    public struct HeroDecisionContext
    {
        public Vector2 heroCenter;          // cell-space center of the hero footprint
        public Vector2 bossCenter;          // cell-space center of the boss footprint
        public Vector2 kiteDir;             // unit vector the kite solver produced this frame
        public float   distanceToBossCells; // straight-line cell distance hero→boss
        public bool    bossInPunishWindow;  // boss has an attack in Recovery or Cooldown
        public bool    bossIsExecutingAttack; // boss has an attack in Windup or Active — dodge cue
    }
}
