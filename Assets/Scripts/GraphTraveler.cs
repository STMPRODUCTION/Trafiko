using UnityEngine;
using System.Collections.Generic;
using System.IO;

public class GraphTraveler : MonoBehaviour
{
    [System.Serializable]
    public class PathData
    {
        public List<Vector3> points = new List<Vector3>();
    }

    public float speed = 2f;
    public float stopDistance = 2f; // Minimum distance to keep from another traveler
    public string trafficLightTag = "TrafficLight";

    private PathData loadedPath;
    private int currentIndex = 0;
    private bool isMoving = false;

    private void Start()
    {
        LoadRandomPath();
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

        // Check for obstacles
        if (IsTravelerTooClose() || IsRedLightAhead())
        {
            return; // Stop movement
        }

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

    private bool IsTravelerTooClose()
    {
        RaycastHit hit;
        if (Physics.Raycast(transform.position, transform.forward, out hit, stopDistance))
        {
            if (hit.collider.gameObject.layer == LayerMask.NameToLayer("Traveler"))
            {
                return true;
            }
        }
        return false;
    }

    private bool IsRedLightAhead()
    {
        RaycastHit hit;
        if (Physics.Raycast(transform.position, transform.forward, out hit, stopDistance * 2))
        {
            if (hit.collider.CompareTag(trafficLightTag))
            {
                TrafficLight light = hit.collider.GetComponent<TrafficLight>();
                if (light != null && !light.isGreen)
                {
                    return true; // Stop if light is red
                }
            }
        }
        return false;
    }

    private void LoadRandomPath()
    {
        string folderPath = Path.Combine(Application.streamingAssetsPath, "Paths");
        if (Directory.Exists(folderPath))
        {
            string[] files = Directory.GetFiles(folderPath, "*.cgraph");
            if (files.Length > 0)
            {
                string randomFile = files[Random.Range(0, files.Length)];
                string json = File.ReadAllText(randomFile);
                loadedPath = JsonUtility.FromJson<PathData>(json);
                Debug.Log($"Loaded random path: {Path.GetFileName(randomFile)} with {loadedPath.points.Count} points.");
            }
            else
            {
                Debug.LogError($"No .cgraph files found in {folderPath}");
            }
        }
        else
        {
            Debug.LogError($"Path folder not found: {folderPath}");
        }
    }
}
