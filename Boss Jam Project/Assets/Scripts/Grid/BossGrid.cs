using System.Collections.Generic;
using BossJam.Configs;
using UnityEngine;

namespace BossJam.GridSystem
{
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

        public int Width => width;
        public int Height => height;
        public float CellSize => cellSize;
        public float TickDuration => config.tickDuration;
        public bool DevMode { get => devMode; set => devMode = value; }

        private readonly Dictionary<Vector2Int, GridFootprint> occupants = new();
        private readonly List<LineRenderer> debugLines = new();
        private Material debugLineMaterial;

        public bool InBounds(Vector2Int cell) =>
            cell.x >= 0 && cell.x < width && cell.y >= 0 && cell.y < height;

        public bool InBounds(Vector2Int anchor, Vector2Int footprint)
        {
            if (footprint.x <= 0 || footprint.y <= 0) return false;
            return anchor.x >= 0 && anchor.y >= 0
                && anchor.x + footprint.x <= width
                && anchor.y + footprint.y <= height;
        }

        public bool IsAreaClear(Vector2Int anchor, Vector2Int footprint, GridFootprint ignore = null)
        {
            if (!InBounds(anchor, footprint)) return false;
            for (int dx = 0; dx < footprint.x; dx++)
            for (int dy = 0; dy < footprint.y; dy++)
            {
                var cell = new Vector2Int(anchor.x + dx, anchor.y + dy);
                if (occupants.TryGetValue(cell, out var occ) && occ != ignore) return false;
            }
            return true;
        }

        public bool Register(GridFootprint entity, Vector2Int anchor)
        {
            if (!IsAreaClear(anchor, entity.Footprint, entity)) return false;
            UnregisterCellsOf(entity);
            for (int dx = 0; dx < entity.Footprint.x; dx++)
            for (int dy = 0; dy < entity.Footprint.y; dy++)
                occupants[new Vector2Int(anchor.x + dx, anchor.y + dy)] = entity;
            return true;
        }

        public void Unregister(GridFootprint entity) => UnregisterCellsOf(entity);

        private void UnregisterCellsOf(GridFootprint entity)
        {
            var stale = new List<Vector2Int>();
            foreach (var kv in occupants)
                if (kv.Value == entity) stale.Add(kv.Key);
            foreach (var c in stale) occupants.Remove(c);
        }

        // Cell (x, y) → world center of that cell on the XZ plane.
        public Vector3 CellToWorld(Vector2Int cell)
        {
            var local = new Vector3((cell.x + 0.5f) * cellSize, 0f, (cell.y + 0.5f) * cellSize);
            return transform.TransformPoint(local);
        }

        // Centre of the rectangle of cells [anchor, anchor+footprint).
        public Vector3 FootprintCenterWorld(Vector2Int anchor, Vector2Int footprint)
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
                return;
            }
            EnsureDebugMaterial();

            int needed = occupants.Count;
            while (debugLines.Count < needed) debugLines.Add(CreateDebugLine());

            int i = 0;
            const float yLift = 0.02f;
            foreach (var kv in occupants)
            {
                var lr = debugLines[i++];
                lr.enabled = true;
                lr.startColor = lr.endColor = devColor;
                lr.startWidth = lr.endWidth = devLineWidth;
                var cell = kv.Key;
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

            // Occupied highlights.
            Gizmos.color = occupiedColor;
            var size = new Vector3(cellSize * 0.95f, 0.02f, cellSize * 0.95f);
            foreach (var kv in occupants)
            {
                var cell = kv.Key;
                var center = new Vector3((cell.x + 0.5f) * cellSize, 0f, (cell.y + 0.5f) * cellSize);
                Gizmos.DrawCube(center, size);
            }

            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
