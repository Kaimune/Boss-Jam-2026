using UnityEngine;

namespace BossJam.Player
{
    [RequireComponent(typeof(Grid))]
    [ExecuteAlways]
    public class GridDebugDrawer : MonoBehaviour
    {
        [SerializeField, Min(1)] private int radius = 6;
        [SerializeField] private Color color = new Color(1f, 1f, 1f, 0.35f);

        private Grid grid;

        private void OnEnable() => grid = GetComponent<Grid>();

        private void Update() => Draw(useGizmoColor: false);

        private void OnDrawGizmos()
        {
            if (grid == null) grid = GetComponent<Grid>();
            Draw(useGizmoColor: true);
        }

        private void Draw(bool useGizmoColor)
        {
            if (grid == null) return;
            if (useGizmoColor) Gizmos.color = color;

            for (int x = -radius; x <= radius; x++)
            {
                for (int y = -radius; y <= radius; y++)
                {
                    var c = new Vector3Int(x, y, 0);
                    var center = grid.GetCellCenterWorld(c);
                    var half = grid.cellSize * 0.5f;

                    // Iso diamond: top, right, bottom, left points of the cell.
                    var top    = center + new Vector3(0,  half.y, 0);
                    var right  = center + new Vector3( half.x, 0, 0);
                    var bottom = center + new Vector3(0, -half.y, 0);
                    var left   = center + new Vector3(-half.x, 0, 0);

                    DrawSegment(top, right, useGizmoColor);
                    DrawSegment(right, bottom, useGizmoColor);
                    DrawSegment(bottom, left, useGizmoColor);
                    DrawSegment(left, top, useGizmoColor);
                }
            }
        }

        private void DrawSegment(Vector3 a, Vector3 b, bool useGizmoColor)
        {
            if (useGizmoColor) Gizmos.DrawLine(a, b);
            else Debug.DrawLine(a, b, color);
        }
    }
}
