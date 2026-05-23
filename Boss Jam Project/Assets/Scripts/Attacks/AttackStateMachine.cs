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
    /// <summary>
    /// Snapshot of the per-phase durations and movement locks for one swing.
    /// Built by the consuming IAttack at TryStart time from its AttackConfig
    /// + the difficulty runtime, then handed to the state machine. Frozen for
    /// the duration of the swing — a debuff applied mid-swing only affects the
    /// next swing.
    /// </summary>
    public struct PhaseTimings
    {
        public float windupSeconds;
        public float activeSeconds;
        public float recoverySeconds;
        public float cooldownSeconds;
        public bool  lockMovementDuringWindup;
        public bool  lockMovementDuringActive;
        public bool  lockMovementDuringRecovery;
    }

    public sealed class AttackStateMachine
    {
        // ---------- Per-phase descriptor ----------
        private readonly struct PhaseInfo
        {
            public readonly AttackState NextOnTimerEnd;
            public readonly Func<PhaseTimings, float> DurationSec;
            public readonly Func<PhaseTimings, bool>  LocksMovement;

            public PhaseInfo(AttackState next, Func<PhaseTimings, float> dur, Func<PhaseTimings, bool> locks)
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
                    dur:   t => t.windupSeconds,
                    locks: t => t.lockMovementDuringWindup) },
                { AttackState.Active, new PhaseInfo(
                    next:  AttackState.Recovery,
                    dur:   t => t.activeSeconds,
                    locks: t => t.lockMovementDuringActive) },
                { AttackState.Recovery, new PhaseInfo(
                    next:  AttackState.Cooldown,
                    dur:   t => t.recoverySeconds,
                    locks: t => t.lockMovementDuringRecovery) },
                { AttackState.Cooldown, new PhaseInfo(
                    next:  AttackState.Idle,
                    dur:   t => t.cooldownSeconds,
                    locks: _ => false) },
            };

        // ---------- Runtime state ----------
        private PhaseTimings timings;
        private bool hasTimings;
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
                if (!hasTimings) return 0f;
                var total = phases[state].DurationSec(timings) * tickScale;
                return total > 0f ? Mathf.Clamp01(1f - (timer / total)) : 0f;
            }
        }

        public bool LocksMovement => hasTimings && phases[state].LocksMovement(timings);

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

        /// <summary>
        /// Provide the per-phase durations + movement locks for the next swing.
        /// Call this every TryStart so the snapshot reflects the current debuff state.
        /// </summary>
        public void Init(PhaseTimings t) { timings = t; hasTimings = true; }

        public void SetTickScale(float m) => tickScale = Mathf.Max(0.0001f, m);

        public bool TryStart()
        {
            if (!hasTimings) return false;
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
            timer = (hasTimings ? phases[next].DurationSec(timings) : 0f) * tickScale;
            StateChanged?.Invoke(prev, next);
            if (onEnter.TryGetValue(next, out var cb)) cb?.Invoke();
        }
    }
}
