using System;
using UnityEngine;

namespace BossJam.Attacks
{
    /// <summary>
    /// Plain C# state machine driving the Idle → Windup → Active → Recovery → Cooldown → Idle
    /// lifecycle for any attack. Each attack MonoBehaviour composes one of these and subscribes
    /// to its phase events. Not a MonoBehaviour, not a base class — pure logic.
    /// </summary>
    public sealed class AttackStateMachine
    {
        private AttackConfig config;
        private float tickScale = 1f;

        private AttackState state = AttackState.Idle;
        private float phaseTimer;
        private float cooldownTimer;

        public AttackState State => state;
        public float CooldownRemaining => Mathf.Max(0f, cooldownTimer);
        public bool IsBusy => state != AttackState.Idle;

        public float PhaseProgress01
        {
            get
            {
                var total = PhaseDuration(state);
                if (total <= 0f) return 0f;
                return Mathf.Clamp01(1f - (phaseTimer / total));
            }
        }

        public bool LocksMovement
        {
            get
            {
                if (config == null) return false;
                switch (state)
                {
                    case AttackState.Windup:   return config.lockMovementDuringWindup;
                    case AttackState.Active:   return config.lockMovementDuringActive;
                    case AttackState.Recovery: return config.lockMovementDuringRecovery;
                    default: return false;
                }
            }
        }

        public event Action OnEnterWindup;
        public event Action OnEnterActive;
        public event Action OnEnterRecovery;
        public event Action OnEnterCooldown;
        public event Action OnEnterIdle;
        public event Action<AttackState, AttackState> StateChanged;

        public void Init(AttackConfig c) => config = c;
        public void SetTickScale(float m) => tickScale = Mathf.Max(0.0001f, m);

        public bool TryStart()
        {
            if (config == null) return false;
            if (state != AttackState.Idle) return false;
            if (cooldownTimer > 0f) return false;
            EnterPhase(AttackState.Windup);
            return true;
        }

        public void Cancel()
        {
            if (state == AttackState.Idle && cooldownTimer <= 0f) return;
            cooldownTimer = 0f;
            EnterPhase(AttackState.Idle);
        }

        public void Tick(float dt)
        {
            if (cooldownTimer > 0f)
            {
                cooldownTimer -= dt;
                if (cooldownTimer < 0f) cooldownTimer = 0f;
            }

            if (state == AttackState.Idle) return;

            if (state == AttackState.Cooldown)
            {
                if (cooldownTimer <= 0f) EnterPhase(AttackState.Idle);
                return;
            }

            phaseTimer -= dt;
            if (phaseTimer > 0f) return;

            switch (state)
            {
                case AttackState.Windup:   EnterPhase(AttackState.Active);   break;
                case AttackState.Active:   EnterPhase(AttackState.Recovery); break;
                case AttackState.Recovery: EnterPhase(AttackState.Cooldown); break;
            }
        }

        private float PhaseDuration(AttackState s)
        {
            if (config == null) return 0f;
            switch (s)
            {
                case AttackState.Windup:   return config.windupSeconds   * tickScale;
                case AttackState.Active:   return config.activeSeconds   * tickScale;
                case AttackState.Recovery: return config.recoverySeconds * tickScale;
                case AttackState.Cooldown: return config.cooldownSeconds * tickScale;
                default: return 0f;
            }
        }

        private void EnterPhase(AttackState next)
        {
            var prev = state;
            state = next;

            switch (next)
            {
                case AttackState.Cooldown:
                    phaseTimer = 0f;
                    cooldownTimer = PhaseDuration(AttackState.Cooldown);
                    break;
                case AttackState.Idle:
                    phaseTimer = 0f;
                    break;
                default:
                    phaseTimer = PhaseDuration(next);
                    break;
            }

            StateChanged?.Invoke(prev, next);
            switch (next)
            {
                case AttackState.Windup:   OnEnterWindup?.Invoke();   break;
                case AttackState.Active:   OnEnterActive?.Invoke();   break;
                case AttackState.Recovery: OnEnterRecovery?.Invoke(); break;
                case AttackState.Cooldown: OnEnterCooldown?.Invoke(); break;
                case AttackState.Idle:     OnEnterIdle?.Invoke();     break;
            }
        }
    }
}
