using System;
using System.Collections.Generic;
using UnityEngine;

namespace BossJam.Attacks
{
    /// <summary>
    /// Plain C# state machine driving the Idle → Windup → Active → Recovery → Cooldown → Idle
    /// lifecycle for any attack. Each attack MonoBehaviour composes one of these and registers
    /// per-phase callbacks via OnEnter. Not a MonoBehaviour, not a base class.
    ///
    /// All per-state data — next-on-timer-end, base duration, movement-lock — lives in the
    /// `phases` dictionary. Every state appears exactly once; there are no switch statements.
    /// </summary>
    public sealed class AttackStateMachine
    {
        // ---------- Per-phase descriptor ----------
        private readonly struct PhaseInfo
        {
            public readonly AttackState NextOnTimerEnd;
            public readonly Func<AttackConfig, float> DurationSec;
            public readonly Func<AttackConfig, bool>  LocksMovement;

            public PhaseInfo(AttackState next, Func<AttackConfig, float> dur, Func<AttackConfig, bool> locks)
            { NextOnTimerEnd = next; DurationSec = dur; LocksMovement = locks; }
        }

        // Full transition graph. Idle's NextOnTimerEnd is a self-loop but Tick short-circuits
        // before reading it — Idle exits via TryStart, not via the timer.
        private static readonly IReadOnlyDictionary<AttackState, PhaseInfo> phases =
            new Dictionary<AttackState, PhaseInfo>
            {
                { AttackState.Idle, new PhaseInfo(
                    next:  AttackState.Idle,
                    dur:   _ => 0f,
                    locks: _ => false) },
                { AttackState.Windup, new PhaseInfo(
                    next:  AttackState.Active,
                    dur:   c => c.windupSeconds,
                    locks: c => c.lockMovementDuringWindup) },
                { AttackState.Active, new PhaseInfo(
                    next:  AttackState.Recovery,
                    dur:   c => c.activeSeconds,
                    locks: c => c.lockMovementDuringActive) },
                { AttackState.Recovery, new PhaseInfo(
                    next:  AttackState.Cooldown,
                    dur:   c => c.recoverySeconds,
                    locks: c => c.lockMovementDuringRecovery) },
                { AttackState.Cooldown, new PhaseInfo(
                    next:  AttackState.Idle,
                    dur:   c => c.cooldownSeconds,
                    locks: _ => false) },
            };

        // ---------- Runtime state ----------
        private AttackConfig config;
        private float tickScale = 1f;

        private AttackState state = AttackState.Idle;
        private float timer;   // unified phase + cooldown timer

        // ---------- Public surface ----------
        public AttackState State => state;
        public float CooldownRemaining => state == AttackState.Cooldown ? Mathf.Max(0f, timer) : 0f;
        public bool IsBusy => state != AttackState.Idle;

        public float PhaseProgress01
        {
            get
            {
                if (config == null) return 0f;
                var total = phases[state].DurationSec(config) * tickScale;
                return total > 0f ? Mathf.Clamp01(1f - (timer / total)) : 0f;
            }
        }

        public bool LocksMovement => config != null && phases[state].LocksMovement(config);

        public event Action<AttackState, AttackState> StateChanged;

        // Per-state entry callbacks. Subscribe via OnEnter(state, handler).
        // Replaces five named events with a single table-driven dispatch.
        private readonly Dictionary<AttackState, Action> onEnter = new Dictionary<AttackState, Action>();

        public void OnEnter(AttackState s, Action handler)
        {
            onEnter.TryGetValue(s, out var existing);
            onEnter[s] = existing + handler;
        }

        public void OffEnter(AttackState s, Action handler)
        {
            if (onEnter.TryGetValue(s, out var existing))
                onEnter[s] = existing - handler;
        }

        public void Init(AttackConfig c) => config = c;
        public void SetTickScale(float m) => tickScale = Mathf.Max(0.0001f, m);

        public bool TryStart()
        {
            if (config == null) return false;
            if (state != AttackState.Idle) return false;
            EnterPhase(AttackState.Windup);
            return true;
        }

        public void Cancel()
        {
            if (state == AttackState.Idle) return;
            EnterPhase(AttackState.Idle);
        }

        /// <summary>
        /// End the current phase immediately, advance per the transition graph
        /// (e.g. Active → Recovery on wall hit). No-op from Idle.
        /// </summary>
        public bool AdvanceNow()
        {
            if (state == AttackState.Idle) return false;
            EnterPhase(phases[state].NextOnTimerEnd);
            return true;
        }

        public void Tick(float dt)
        {
            if (state == AttackState.Idle) return;
            timer -= dt;
            if (timer > 0f) return;
            EnterPhase(phases[state].NextOnTimerEnd);
        }

        // ---------- Internals ----------
        private void EnterPhase(AttackState next)
        {
            var prev = state;
            state = next;
            timer = (config != null ? phases[next].DurationSec(config) : 0f) * tickScale;
            StateChanged?.Invoke(prev, next);
            if (onEnter.TryGetValue(next, out var cb)) cb?.Invoke();
        }
    }
}
