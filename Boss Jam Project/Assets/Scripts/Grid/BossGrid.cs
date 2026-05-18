using System.Collections.Generic;
using BossJam.Configs;
using UnityEngine;

namespace BossJam.GridSystem
{
    /// <summary>
    /// Spatial grid: world bounds, cell size, world-space conversion, dev-mode rendering.
    /// Collision/occupancy logic lives in <see cref="CollisionDetection"/>, owned by this component.
    /// Public Register/Unregister/InBounds delegate to it.
    /// </summary>
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public class BossGrid : MonoBehaviour
    {
        [Header("Bounds")]
        [SerializeField, Min(1)] private int width = 9;
        [SerializeField, Min(1)] private int height = 9;

        [Header("Cell")]
        [Tooltip("Side length of one square cell in world units. Cells lie flat on the XZ plane.")]
        [SerializeField, Min(0.001f)] private float cellSize = 1f;

        [Header("Gizmos")]
        [SerializeField] private Color cellColor = new Color(1f, 1f, 1f, 0.35f);
        [SerializeField] private Color occupiedColor = new Color(1f, 0.3f, 0.3f, 0.45f);

        [Header("Config")]
        [SerializeField] private GameConfig config;

        [Header("Dev Mode")]
        [Tooltip("Show red borders around occupied cells in the Game view. Toggle with F1 at runtime.")]
        [SerializeField] private bool devMode = true;
        [SerializeField] private Color devColor = new Color(1f, 0.15f, 0.15f, 1f);
        [SerializeField, Min(0.005f)] private float devLineWidth = 0.06f;
        [Tooltip("Also draw all cell borders in the Game view (not just occupied ones). Driven by devMode.")]
        [SerializeField] private bool showAllCells = true;
        [SerializeField] private Color allCellsColor = new Color(1f, 1f, 1f, 0.15f);
        [SerializeField, Min(0.001f)] private float allCellsLineWidth = 0.015f;

        public int Width => width;
        public int Height => height;
        public float CellSize => cellSize;
        public float TickDuration => config.tickDuration;
        public bool DevMode { get => devMode; set => devMode = value; }

        // Cell-occupancy + verdict-resolution. Lazy so it sees up-to-date bounds when first used.
        private CollisionDetection collisions;
        public CollisionDetection Collisions
        {
            get
            {
                if (collisions == null) collisions = new CollisionDetection(width, height);
                return collisions;
            }
        }

        private readonly List<LineRenderer> debugLines = new();
        private readonly List<LineRenderer> gridLines = new();
        private int gridLinesBuiltForWidth = -1;
        private int gridLinesBuiltForHeight = -1;
        private float gridLinesBuiltForCellSize = -1f;
        private Material debugLineMaterial;

        // ---------------------------------------------------------------- collision API (delegates)

        public bool InBounds(Vector2Int cell) => Collisions.InBoundsCell(cell);
        public bool InBounds(Vector2 anchor, Vector2 footprint) => Collisions.InBounds(anchor, footprint);
        public bool Register(GridFootprint entity, Vector2 anchor) => Collisions.Register(entity, anchor);
        public void Unregister(GridFootprint entity) => Collisions.Unregister(entity);

        // ---------------------------------------------------------------- world-space helpers

        // Cell (x, y) → world center of that cell on the XZ plane.
        public Vector3 CellToWorld(Vector2Int cell)
        {
            var local = new Vector3((cell.x + 0.5f) * cellSize, 0f, (cell.y + 0.5f) * cellSize);
            return transform.TransformPoint(local);
        }

        // Centre of the rectangle [anchor, anchor+footprint). Anchor may be fractional.
        public Vector3 FootprintCenterWorld(Vector2 anchor, Vector2 footprint)
        {
            float cx = anchor.x + footprint.x * 0.5f;
            float cy = anchor.y + footprint.y * 0.5f;
            return transform.TransformPoint(new Vector3(cx * cellSize, 0f, cy * cellSize));
        }

        public Vector2Int WorldToCell(Vector3 world)
        {
            var local = transform.InverseTransformPoint(world);
            return new Vector2Int(
                Mathf.FloorToInt(local.x / cellSize),
                Mathf.FloorToInt(local.z / cellSize));
        }

        // ---------------------------------------------------------------- lifecycle / debug

        private void OnValidate()
        {
            // Bounds may have changed in the inspector — drop the cached ledger so it
            // gets recreated with the new dimensions on next access.
            collisions = null;
        }

        private void Update()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.f1Key.wasPressedThisFrame) devMode = !devMode;
            UpdateDebugLines();
        }

        private void UpdateDebugLines()
        {
            if (!devMode)
            {
                foreach (var lr in debugLines) if (lr != null) lr.enabled = false;
                foreach (var lr in gridLines) if (lr != null) lr.enabled = false;
                return;
            }
            EnsureDebugMaterial();
            UpdateAllCellsLines();

            int needed = Collisions.OccupiedCellCount;
            while (debugLines.Count < needed) debugLines.Add(CreateDebugLine());

            int i = 0;
            const float yLift = 0.02f;
            foreach (var cell in Collisions.OccupiedCells)
            {
                var lr = debugLines[i++];
                lr.enabled = true;
                lr.startColor = lr.endColor = devColor;
                lr.startWidth = lr.endWidth = devLineWidth;
                lr.SetPosition(0, transform.TransformPoint(new Vector3(cell.x * cellSize,       yLift, cell.y * cellSize)));
                lr.SetPosition(1, transform.TransformPoint(new Vector3((cell.x + 1) * cellSize, yLift, cell.y * cellSize)));
                lr.SetPosition(2, transform.TransformPoint(new Vector3((cell.x + 1) * cellSize, yLift, (cell.y + 1) * cellSize)));
                lr.SetPosition(3, transform.TransformPoint(new Vector3(cell.x * cellSize,       yLift, (cell.y + 1) * cellSize)));
            }
            for (; i < debugLines.Count; i++) if (debugLines[i] != null) debugLines[i].enabled = false;
        }

        private LineRenderer CreateDebugLine()
        {
            var go = new GameObject("BossGridDebugCell") { hideFlags = HideFlags.HideAndDontSave };
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.loop = true;
            lr.positionCount = 4;
            lr.material = debugLineMaterial;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            return lr;
        }

        // Row + column lines forming the full cell grid. Rebuilt only when size changes.
        private void UpdateAllCellsLines()
        {
            if (!showAllCells)
            {
                foreach (var lr in gridLines) if (lr != null) lr.enabled = false;
                return;
            }
            EnsureGridLines();
            foreach (var lr in gridLines)
            {
                if (lr == null) continue;
                lr.enabled = true;
                lr.startColor = lr.endColor = allCellsColor;
                lr.startWidth = lr.endWidth = allCellsLineWidth;
            }
        }

        private void EnsureGridLines()
        {
            if (width == gridLinesBuiltForWidth
                && height == gridLinesBuiltForHeight
                && Mathf.Approximately(cellSize, gridLinesBuiltForCellSize)
                && gridLines.Count == (width + 1) + (height + 1))
                return;

            foreach (var lr in gridLines) if (lr != null) DestroyDebugChild(lr.gameObject);
            gridLines.Clear();

            const float yLift = 0.01f;
            float w = width * cellSize;
            float h = height * cellSize;

            for (int x = 0; x <= width; x++)
            {
                var lr = CreateGridLine();
                lr.SetPosition(0, transform.TransformPoint(new Vector3(x * cellSize, yLift, 0f)));
                lr.SetPosition(1, transform.TransformPoint(new Vector3(x * cellSize, yLift, h)));
                gridLines.Add(lr);
            }
            for (int y = 0; y <= height; y++)
            {
                var lr = CreateGridLine();
                lr.SetPosition(0, transform.TransformPoint(new Vector3(0f, yLift, y * cellSize)));
                lr.SetPosition(1, transform.TransformPoint(new Vector3(w,  yLift, y * cellSize)));
                gridLines.Add(lr);
            }

            gridLinesBuiltForWidth = width;
            gridLinesBuiltForHeight = height;
            gridLinesBuiltForCellSize = cellSize;
        }

        private LineRenderer CreateGridLine()
        {
            var go = new GameObject("BossGridLine") { hideFlags = HideFlags.HideAndDontSave };
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.loop = false;
            lr.positionCount = 2;
            lr.material = debugLineMaterial;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            return lr;
        }

        private void DestroyDebugChild(GameObject go)
        {
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }

        private void EnsureDebugMaterial()
        {
            if (debugLineMaterial != null) return;
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            debugLineMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            if (debugLineMaterial.HasColor("_BaseColor")) debugLineMaterial.SetColor("_BaseColor", devColor);
            if (debugLineMaterial.HasColor("_Color")) debugLineMaterial.SetColor("_Color", devColor);
        }

        private void OnDestroy()
        {
            if (debugLineMaterial != null)
            {
#if UNITY_EDITOR
                if (Application.isPlaying) Destroy(debugLineMaterial); else DestroyImmediate(debugLineMaterial);
#else
                Destroy(debugLineMaterial);
#endif
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.matrix = transform.localToWorldMatrix;

            // Cell outlines.
            Gizmos.color = cellColor;
            for (int x = 0; x <= width; x++)
                Gizmos.DrawLine(new Vector3(x * cellSize, 0, 0), new Vector3(x * cellSize, 0, height * cellSize));
            for (int y = 0; y <= height; y++)
                Gizmos.DrawLine(new Vector3(0, 0, y * cellSize), new Vector3(width * cellSize, 0, y * cellSize));

            // Occupied highlights (collisions may be null in editor before first use).
            if (collisions != null)
            {
                Gizmos.color = occupiedColor;
                var size = new Vector3(cellSize * 0.95f, 0.02f, cellSize * 0.95f);
                foreach (var cell in collisions.OccupiedCells)
                {
                    var center = new Vector3((cell.x + 0.5f) * cellSize, 0f, (cell.y + 0.5f) * cellSize);
                    Gizmos.DrawCube(center, size);
                }
            }

            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
