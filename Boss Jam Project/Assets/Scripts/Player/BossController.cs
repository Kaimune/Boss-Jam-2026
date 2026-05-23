using System.Collections.Generic;
using BossJam.Attacks;
using BossJam.Difficulty;
using BossJam.Game;
using BossJam.GridSystem;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace BossJam.Player
{
    [RequireComponent(typeof(GridMover))]
    public class BossController : MonoBehaviour, IGridEntity, IDamageable, IInvulnerable
    {
        [Header("Tick")]
        [Tooltip("Per-actor scalar on the grid's tick. 1 = baseline, >1 slower, <1 faster.")]
        [SerializeField, Min(0.01f)] private float tickMultiplier = 1f;

        [Header("HP")]
        [SerializeField, Min(1), FormerlySerializedAs("hp")] private int maxHp = 10;
        private int currentHp;
        private int spawnedMaxHp;

        public int MaxHp => spawnedMaxHp;
        public int CurrentHp => currentHp;
        public bool IsDead => isDead;
        public event System.Action<int, int> HpChanged;
        public event System.Action BossDied;

        private bool isDead;

        private DifficultyRuntime rt;
        private float Eff(Target t, float b) => rt != null ? rt.Get(t, b) : b;
        private int   EffI(Target t, int b)  => rt != null ? rt.GetInt(t, b) : b;

        [Header("Input")]
        [Tooltip("Below this magnitude the stick is treated as released.")]
        [SerializeField, Range(0f, 0.9f)] private float deadzone = 0.3f;

        [Header("Facing")]
        [Tooltip("Transform that rotates to face movement. Leave null to rotate the root.")]
        [SerializeField] private Transform visual;
        [SerializeField, Min(0f)] private float turnDegreesPerSecond = 720f;
        [Tooltip("Yaw offset (degrees) applied to the visual. Spin this until the model's nose points along movement direction.")]
        [SerializeField, Range(-180f, 180f)] private float modelYawOffset = 0f;

        private Quaternion facingTarget = Quaternion.identity;

        private GridFootprint cachedFootprint;
        public GridFootprint Footprint =>
            cachedFootprint != null ? cachedFootprint : (cachedFootprint = GetComponent<GridFootprint>());
        public float TickMultiplier => Eff(Target.BossTickMultiplier, tickMultiplier);
        public Team Team => Team.Boss;

        public Verdict OnEnteredBy(IGridEntity mover)
        {
            // Our own hitboxes overlap our footprint — pass through, no effect.
            if (mover != null && mover.Team == Team.Boss) return Verdict.Pass;

            // Hostile projectile (Team.Enemy) → pass, damage us, destroy it.
            if (mover != null && mover.Team == Team.Enemy)
            {
                int damage = (mover is IDamageDealer dd) ? dd.Damage : 1;
                return Verdict.PassWith(() =>
                {
                    TakeDamage(damage, mover);
                    if (mover is MonoBehaviour mb && mb != null) Destroy(mb.gameObject);
                });
            }
            // Hero units → solid; the boss must use attacks to clear them.
            return Verdict.Block;
        }

        // Attacks living on child GameObjects gate movement during windup/active via this flag.
        // Empty list (no attacks attached) → never locked.
        private List<IAttack> attacks = new List<IAttack>();
        public bool IsMovementLockedByAttack
        {
            get
            {
                for (int i = 0; i < attacks.Count; i++)
                    if (attacks[i] != null && attacks[i].LocksMovement) return true;
                return false;
            }
        }

        // True when any of the boss's attacks is in Recovery or Cooldown — the
        // hero treats this as the "punish window" to dive in for melee while
        // the boss is locked out of an immediate response.
        public bool InPunishWindow
        {
            get
            {
                for (int i = 0; i < attacks.Count; i++)
                {
                    var a = attacks[i];
                    if (a == null) continue;
                    var s = a.State;
                    if (s == AttackState.Recovery || s == AttackState.Cooldown) return true;
                }
                return false;
            }
        }

        public void TakeDamage(int amount, IGridEntity source)
        {
            if (isDead) return;
            if (IsInvulnerable) return;
            currentHp = Mathf.Max(0, currentHp - amount);
            Debug.Log($"Boss took {amount} damage (hp={currentHp}, from {source})");
            HpChanged?.Invoke(currentHp, spawnedMaxHp);
            rt?.RaiseBossDamaged(amount, source);
            if (currentHp <= 0) Die();
        }

        // Time.time at which i-frames lapse. Set via SetInvulnFor by attacks that
        // grant temporary invulnerability (e.g. the charge slam).
        private float invulnUntil = -1f;
        public bool IsInvulnerable => Time.time < invulnUntil;

        /// <summary>
        /// Grant invulnerability for at least `seconds` from now. If a longer window
        /// is already active, keeps the existing one — invuln only ever extends.
        /// </summary>
        public void SetInvulnFor(float seconds)
        {
            if (seconds <= 0f) return;
            var until = Time.time + seconds;
            if (until > invulnUntil) invulnUntil = until;
        }

        private void Die()
        {
            isDead = true;
            Debug.Log("Boss died — entering GameOver.");
            BossDied?.Invoke();
            // GameStateController handles the pause + GameOver screen; the
            // boss is brought back via Respawn() when the player presses Space.
            if (gameState != null) gameState.TriggerGameOver();
            else Respawn(); // fallback if no controller in scene
        }

        /// <summary>
        /// Restore the boss to a fresh state for another life. Re-snapshots
        /// MaxHp through the difficulty runtime so any debuffs that landed
        /// during the previous life are baked into the new spawn.
        /// </summary>
        public void Respawn()
        {
            isDead = false;
            spawnedMaxHp = EffI(Target.BossMaxHp, maxHp);
            currentHp = spawnedMaxHp;
            HpChanged?.Invoke(currentHp, spawnedMaxHp);
            enabled = true;
        }

        private void ApplyTick()
        {
            float tickMul = Eff(Target.BossTickMultiplier, tickMultiplier);
            foreach (var t in GetComponentsInChildren<ITickScalable>(includeInactive: true))
                t.ApplyTick(tickMul);
        }

        private void OnValidate() => ApplyTick();

        private GridMover mover;
        private InputAction moveAction;
        private InputAction primaryAction;
        private InputAction secondaryAction;
        private InputAction ultAction;

        // Cached so attacks can aim where the boss is currently facing, even when input is zero.
        private Vector3 aimForward = Vector3.forward;
        public Vector3 AimForward => aimForward;

        private GameStateController gameState;

        private void Awake()
        {
            rt = FindFirstObjectByType<DifficultyRuntime>();
            if (rt != null) rt.Boss = this;

            // Find the controller now, but defer registration until after
            // InputActions are built — the setter on Boss can flip our
            // enabled flag, which would fire OnDisable before init.
            gameState = FindFirstObjectByType<GameStateController>();

            // Snapshot effective MaxHp at spawn — debuffs that later raise/lower
            // BossMaxHp don't retroactively change the live boss's max.
            spawnedMaxHp = EffI(Target.BossMaxHp, maxHp);
            currentHp = spawnedMaxHp;

            mover = GetComponent<GridMover>();
            moveAction = BuildMoveAction();
            primaryAction   = new InputAction(name: "AttackPrimary",   type: InputActionType.Button, binding: "<Keyboard>/j");
            secondaryAction = new InputAction(name: "AttackSecondary", type: InputActionType.Button, binding: "<Keyboard>/k");
            ultAction       = new InputAction(name: "AttackUlt",       type: InputActionType.Button, binding: "<Keyboard>/l");
            if (visual == null) visual = transform;
            // Honour the scene-authored idle rotation as the starting facing.
            // `modelYawOffset` is reserved for compensating models that are built
            // with a non-standard forward axis — it is applied only when
            // movement input drives the boss to LookRotation a new direction.
            facingTarget = visual.rotation;
            GetComponentsInChildren<IAttack>(includeInactive: true, attacks);

            // Safe to register now — if the setter flips enabled=false, the
            // resulting OnDisable will find the InputActions ready to disable.
            if (gameState != null) gameState.Boss = this;
        }

        private void OnEnable()
        {
            ApplyTick();
            moveAction.Enable();
            primaryAction.Enable();
            secondaryAction.Enable();
            ultAction.Enable();
        }

        private void OnDisable()
        {
            moveAction.Disable();
            primaryAction.Disable();
            secondaryAction.Disable();
            ultAction.Disable();
        }

        private void Start()
        {
            HpChanged?.Invoke(currentHp, spawnedMaxHp);
        }

        private void OnDestroy()
        {
            if (rt != null && rt.Boss == this) rt.Boss = null;
            if (gameState != null && gameState.Boss == this) gameState.Boss = null;
            moveAction?.Dispose();
            primaryAction?.Dispose();
            secondaryAction?.Dispose();
            ultAction?.Dispose();
        }

        private void Update()
        {
            var raw = moveAction.ReadValue<Vector2>();
            var dir = IsMovementLockedByAttack ? Vector2.zero : ToDirection(raw);
            mover.InputDirection = dir;

            if (dir.sqrMagnitude > 0.0001f)
            {
                var forward = new Vector3(dir.x, 0f, dir.y);
                aimForward = forward;
                facingTarget = Quaternion.LookRotation(forward, Vector3.up) * Quaternion.Euler(0f, modelYawOffset, 0f);
            }

            visual.rotation = Quaternion.RotateTowards(
                visual.rotation,
                facingTarget,
                turnDegreesPerSecond * Time.deltaTime);

            if (primaryAction.WasPressedThisFrame())   FireHotkey(AttackHotkey.Primary);
            if (secondaryAction.WasPressedThisFrame()) FireHotkey(AttackHotkey.Secondary);
            if (ultAction.WasPressedThisFrame())       FireHotkey(AttackHotkey.Ult);
        }

        private void FireHotkey(AttackHotkey hotkey)
        {
            // Aim point is just "boss position + facing forward"; attacks subtract & normalize.
            var aim = transform.position + aimForward;
            for (int i = 0; i < attacks.Count; i++)
            {
                var a = attacks[i];
                if (a == null || a.Config == null) continue;
                if (a.Config.hotkey != hotkey) continue;
                a.TryStart(aim);
                return;
            }
        }

        // 8-way snap with normalization (so diagonals don't move faster than cardinal).
        private Vector2 ToDirection(Vector2 raw)
        {
            if (raw.sqrMagnitude < deadzone * deadzone) return Vector2.zero;
            var x = Mathf.Abs(raw.x) > deadzone ? Mathf.Sign(raw.x) : 0f;
            var y = Mathf.Abs(raw.y) > deadzone ? Mathf.Sign(raw.y) : 0f;
            var dir = new Vector2(x, y);
            return dir.sqrMagnitude > 0f ? dir.normalized : Vector2.zero;
        }

        private static InputAction BuildMoveAction()
        {
            var action = new InputAction(name: "Move", type: InputActionType.Value, expectedControlType: "Vector2");

            action.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");

            action.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");

            action.AddBinding("<Gamepad>/leftStick");
            action.AddCompositeBinding("2DVector")
                .With("Up", "<Gamepad>/dpad/up")
                .With("Down", "<Gamepad>/dpad/down")
                .With("Left", "<Gamepad>/dpad/left")
                .With("Right", "<Gamepad>/dpad/right");

            return action;
        }
    }
}
