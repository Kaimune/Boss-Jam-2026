using BossJam.Difficulty;
using BossJam.Enemies;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BossJam.Game
{
    /// <summary>
    /// Dev tool: P instakills the hero (AI enemy), O drops it to 1 HP.
    /// Active only when RunState.debugHeroHpKeys is on.
    /// Damage goes through HeroEnemy.TakeDamage so HpChanged + the death/
    /// save-scum path fire normally; iframes and non-Playing states still
    /// block (intentional).
    /// </summary>
    public class DebugBossHp : MonoBehaviour
    {
        [SerializeField] private Key instakillKey = Key.P;
        [SerializeField] private Key oneHpKey = Key.O;

        private void Update()
        {
            var rt = FindFirstObjectByType<DifficultyRuntime>();
            if (rt == null || rt.State == null || !rt.State.debugHeroHpKeys) return;

            var kb = Keyboard.current;
            if (kb == null) return;

            bool kill = kb[instakillKey].wasPressedThisFrame;
            bool oneHp = kb[oneHpKey].wasPressedThisFrame;
            if (!kill && !oneHp) return;

            var hero = FindFirstObjectByType<HeroEnemy>();
            if (hero == null) return;

            if (kill)
            {
                hero.TakeDamage(99999, null);
                return;
            }

            int hp = hero.CurrentHp;
            if (hp > 1) hero.TakeDamage(hp - 1, null);
        }
    }
}
