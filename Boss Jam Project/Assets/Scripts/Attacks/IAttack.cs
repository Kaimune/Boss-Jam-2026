using System;
using UnityEngine;

namespace BossJam.Attacks
{
    public interface IAttack
    {
        AttackConfig Config { get; }
        AttackState State { get; }

        float PhaseProgress01 { get; }
        float CooldownRemaining { get; }
        float TimeToIdle { get; }

        bool IsBusy { get; }
        bool LocksMovement { get; }

        bool TryStart(Vector3 aimWorldPoint);
        void Cancel();

        event Action<AttackState, AttackState> StateChanged;
    }
}
