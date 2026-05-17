using UnityEngine;

public class BossSlashSpawner : MonoBehaviour
{
    public GameObject slashPrefab;
    public Transform slashSpawnPoint;
    public Camera mainCamera;

    public void SpawnForwardSlash()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        GameObject slash = Instantiate(
            slashPrefab,
            slashSpawnPoint.position,
            slashSpawnPoint.rotation
        );

        // Detach into world space, so it does not keep rotating with the boss
        slash.transform.SetParent(null);

        // Optional: make it face camera after spawning
        slash.transform.rotation = Quaternion.LookRotation(
            mainCamera.transform.forward,
            Vector3.up
        );
    }
}