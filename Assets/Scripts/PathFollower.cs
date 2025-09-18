using UnityEngine;
using System.Collections.Generic;
using System.IO;

public class PathFollower : MonoBehaviour
{
    [System.Serializable]
    public class PathData
    {
        public List<Vector3> points = new List<Vector3>();
    }

    public string pathFileName; // Example: "path.cgraph"
    public float speed = 2f;

    private PathData loadedPath;
    private int currentIndex = 0;
    private bool isMoving = false;

    private void Start()
    {
        LoadPath();
        if (loadedPath != null && loadedPath.points.Count > 0)
        {
            transform.position = loadedPath.points[0];
            currentIndex = 0;
            isMoving = true;
        }
    }

    private void Update()
    {
        if (!isMoving || loadedPath == null || loadedPath.points.Count < 2) return;

        Vector3 target = loadedPath.points[currentIndex + 1];
        transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);

        if (Vector3.Distance(transform.position, target) < 0.01f)
        {
            currentIndex++;
            if (currentIndex >= loadedPath.points.Count - 1)
            {
                isMoving = false; // Reached end of path
            }
        }
    }

    private void LoadPath()
    {
        string fullPath = Path.Combine(Application.dataPath, pathFileName);
        if (File.Exists(fullPath))
        {
            string json = File.ReadAllText(fullPath);
            loadedPath = JsonUtility.FromJson<PathData>(json);
            Debug.Log($"Loaded path with {loadedPath.points.Count} points.");
        }
        else
        {
            Debug.LogError($"Path file not found: {fullPath}");
        }
    }
}
