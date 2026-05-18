using UnityEngine;

namespace BossJam.GridSystem
{
    [RequireComponent(typeof(GridFootprint))]
    [DefaultExecutionOrder(-100)]
    public class AutoGridEntity : MonoBehaviour
    {
        [Tooltip("Footprint size in cell units passed to GridFootprint.Configure. Matches the prefab's intended occupancy.")]
        [SerializeField] private Vector2 footprintSize = new Vector2(1f, 1f);

        private void Awake()
        {
            var grid = FindFirstObjectByType<BossGrid>();
            if (grid == null)
            {
                Debug.LogWarning($"{nameof(AutoGridEntity)} on '{name}': no BossGrid in scene.", this);
                return;
            }
            var cell = grid.WorldToCell(transform.position);
            var fp = GetComponent<GridFootprint>();
            fp.Configure(new Vector2(cell.x, cell.y), footprintSize, grid);
        }
    }
}
