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

        [Header("Input")]
        [Tooltip("Below this magnitude the stick is treated as released.")]
        [SerializeField, Range(0f, 0.9f)] private float deadzone = 0.3f;

        private GridFootprint cachedFootprint;
        public GridFootprint Footprint =>
            cachedFootprint != null ? cachedFootprint : (cachedFootprint = GetComponent<GridFootprint>());
        public float TickMultiplier => tickMultiplier;
        public Team Team => Team.Boss;
        public Verdict OnEnteredBy(IGridEntity mover) => Verdict.Block;

        private void ApplyTick()
        {
            foreach (var t in GetComponentsInChildren<ITickScalable>(includeInactive: true))
                t.ApplyTick(tickMultiplier);
        }

        private void OnValidate() => ApplyTick();

        private GridMover mover;
        private InputAction moveAction;

        private void Awake()
        {
            mover = GetComponent<GridMover>();
            moveAction = BuildMoveAction();
        }

        private void OnEnable()
        {
            ApplyTick();
            moveAction.Enable();
        }

        private void OnDisable()
        {
            moveAction.Disable();
        }

        private void OnDestroy()
        {
            moveAction?.Dispose();
        }

        private void Update()
        {
            var raw = moveAction.ReadValue<Vector2>();
            mover.InputDirection = ToDirection(raw);
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
