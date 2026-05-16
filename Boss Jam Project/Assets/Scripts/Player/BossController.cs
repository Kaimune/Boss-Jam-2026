using BossJam.GridSystem;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BossJam.Player
{
    [RequireComponent(typeof(GridMover))]
    public class BossController : MonoBehaviour, IGridEntity
    {
        [Header("Tick")]
        [Tooltip("Per-actor scalar on the grid's tick. 1 = baseline, >1 slower, <1 faster.")]
        [SerializeField, Min(0.01f)] private float tickMultiplier = 1f;

        [Header("Auto-repeat")]
        [Tooltip("Pause after the first step finishes before auto-stepping again while held.")]
        [SerializeField, Min(0f)] private float initialDelay = 0.15f;

        [Header("Input")]
        [Tooltip("Below this magnitude the stick is treated as released.")]
        [SerializeField, Range(0f, 0.9f)] private float deadzone = 0.3f;

        private GridFootprint cachedFootprint;
        public GridFootprint Footprint =>
            cachedFootprint != null ? cachedFootprint : (cachedFootprint = GetComponent<GridFootprint>());
        public float TickMultiplier => tickMultiplier;

        private void ApplyTick()
        {
            foreach (var t in GetComponentsInChildren<ITickScalable>(includeInactive: true))
                t.ApplyTick(tickMultiplier);
        }

        private void OnValidate() => ApplyTick();

        private GridMover mover;
        private InputAction moveAction;
        private float repeatReadyAt;
        private bool initialPauseConsumed;

        private void Awake()
        {
            mover = GetComponent<GridMover>();
            moveAction = BuildMoveAction();
        }

        private void OnEnable()
        {
            ApplyTick();
            moveAction.Enable();
            mover.StepCompleted += OnStepCompleted;
        }

        private void OnDisable()
        {
            moveAction.Disable();
            mover.StepCompleted -= OnStepCompleted;
        }

        private void OnDestroy()
        {
            moveAction?.Dispose();
        }

        private void Update()
        {
            if (mover.IsMoving) return;
            if (Time.time < repeatReadyAt) return;

            var raw = moveAction.ReadValue<Vector2>();
            var delta = ToGridDelta(raw);
            if (delta == Vector2Int.zero)
            {
                // Released: next press starts a fresh initial-delay cycle.
                initialPauseConsumed = false;
                return;
            }

            mover.TryStep(delta);
        }

        private void OnStepCompleted()
        {
            // After the very first step in a hold, pause once. Subsequent steps
            // chain back-to-back for fluid traversal.
            if (initialPauseConsumed) return;

            var raw = moveAction.ReadValue<Vector2>();
            if (ToGridDelta(raw) != Vector2Int.zero)
            {
                repeatReadyAt = Time.time + initialDelay;
                initialPauseConsumed = true;
            }
        }

        private Vector2Int ToGridDelta(Vector2 raw)
        {
            if (raw.sqrMagnitude < deadzone * deadzone) return Vector2Int.zero;

            // 4-direction: pick the dominant axis only.
            if (Mathf.Abs(raw.x) > Mathf.Abs(raw.y))
            {
                return new Vector2Int(raw.x > 0 ? 1 : -1, 0);
            }
            return new Vector2Int(0, raw.y > 0 ? 1 : -1);
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
