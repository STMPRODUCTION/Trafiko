using UnityEngine;

public class TravelerSpawner : MonoBehaviour
{
    [Header("Spawner Settings")]
    public GameObject prefab;
    public float spawnInterval = 2f;
    public Transform spawnPoint;

    private float timer;

    private void Update()
    {
        timer += Time.deltaTime;
        if (timer >= spawnInterval)
        {
            SpawnObject();
            timer = 0f;
        }
    }

    private void SpawnObject()
    {
        Transform spawnTransform = spawnPoint != null ? spawnPoint : transform;
        Instantiate(prefab, spawnTransform.position, spawnTransform.rotation);
    }
}