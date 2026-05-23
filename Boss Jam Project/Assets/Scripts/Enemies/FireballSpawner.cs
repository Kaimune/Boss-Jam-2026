using BossJam.GridSystem;
using UnityEngine;

namespace BossJam.Enemies
{
    public class FireballSpawner : MonoBehaviour
    {
        [SerializeField] private Fireball fireballPrefab;
        [SerializeField] private BossGrid grid;
        [SerializeField] private Transform target;
        [SerializeField] private Vector2 spawnAnchor = new Vector2(20f, 20f);
        [SerializeField] private Vector2 fireballSize = Vector2.one;
        [SerializeField] private float intervalSeconds = 5f;

        private float nextSpawnTime;

        private void Start()
        {
            nextSpawnTime = Time.time + intervalSeconds;
        }

        private void Update()
        {
            if (BossJam.Game.GameStateController.Instance != null &&
                BossJam.Game.GameStateController.Instance.State != BossJam.Game.GameState.Playing)
                return;
            if (fireballPrefab == null || grid == null || target == null) return;
            if (Time.time < nextSpawnTime) return;
            nextSpawnTime = Time.time + intervalSeconds;
            Spawn();
        }

        private void Spawn()
        {
            Vector3 worldPos = grid.FootprintCenterWorld(spawnAnchor, fireballSize);

            Vector3 d3 = target.position - worldPos;
            Vector2 dir = new Vector2(d3.x, d3.z);
            if (dir.sqrMagnitude < 0.0001f) dir = Vector2.left;
            dir.Normalize();

            Fireball instance = Instantiate(fireballPrefab);
            instance.gameObject.SetActive(false);
            instance.transform.position = worldPos;

            GridFootprint fp = instance.GetComponent<GridFootprint>();
            if (fp != null) fp.Configure(spawnAnchor, fireballSize, grid);

            instance.Direction = dir;
            instance.gameObject.SetActive(true);
        }
    }
}
