using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class TrafficStatsLogger : MonoBehaviour
{
    // Dictionary to store instances per scene
    private static Dictionary<Scene, TrafficStatsLogger> sceneInstances = new Dictionary<Scene, TrafficStatsLogger>();
    
    [SerializeField] private TrafficLightAgent agent;

    [SerializeField] public float totalWaitTime = 0f;
    [SerializeField] public float avrageWaitTime;
    [SerializeField] public float avgCarSpeed;
    [SerializeField] public float recentAvgWaitTime;
    [SerializeField] public float recentAvgSpeed;

    private float lastReportTime = 0f;
    public float TimeSinceLastReport => Time.time - lastReportTime;

    [SerializeField] public int carsReported = 0;

    [SerializeField]
    public List<LaneCount> carsPerLaneList = new List<LaneCount>()
    {
        new LaneCount { laneID = "N", carCount = 0 },
        new LaneCount { laneID = "S", carCount = 0 },
        new LaneCount { laneID = "E", carCount = 0 },
        new LaneCount { laneID = "V", carCount = 0 }
    };

    private Dictionary<string, int> carsPerLane = new Dictionary<string, int>();
    private float totalIntersectionSpeed = 0f;
    private int speedSamples = 0;

    // Queues to store last 5 values
    private Queue<float> recentWaitTimes = new Queue<float>();
    private Queue<float> recentSpeeds = new Queue<float>();
    private const int maxRecent = 5;

    [Header("Simulation Speed")]
    [Range(0.1f, 100f)]
    public float simulationTimeScale = 1f;
    
    // Scene-specific instance getter
    public static TrafficStatsLogger GetInstanceForScene(Scene scene)
    {
        if (sceneInstances.ContainsKey(scene))
            return sceneInstances[scene];
        return null;
    }
    
    // Get instance for current scene
    public static TrafficStatsLogger GetCurrentSceneInstance()
    {
        Scene currentScene = SceneManager.GetActiveScene();
        return GetInstanceForScene(currentScene);
    }
    
    // Get instance for the scene this GameObject belongs to
    public static TrafficStatsLogger GetInstanceForGameObject(GameObject go)
    {
        return GetInstanceForScene(go.scene);
    }

    void Awake()
    {
        Scene myScene = gameObject.scene;
        
        // Check if there's already an instance for this scene
        if (sceneInstances.ContainsKey(myScene))
        {
            // There's already an instance for this scene, destroy this one
            Destroy(gameObject);
            return;
        }
        
        // Register this instance for this scene
        sceneInstances[myScene] = this;

        // Sync list to dictionary
        foreach (var lane in carsPerLaneList)
        {
            carsPerLane[lane.laneID] = lane.carCount;
        }
    }

    void OnDestroy()
    {
        // Clean up the reference when destroyed
        Scene myScene = gameObject.scene;
        if (sceneInstances.ContainsKey(myScene) && sceneInstances[myScene] == this)
        {
            sceneInstances.Remove(myScene);
        }
    }

    void SyncDictionaryToList()
    {
        foreach (var lane in carsPerLaneList)
        {
            if (carsPerLane.ContainsKey(lane.laneID))
                lane.carCount = carsPerLane[lane.laneID];
        }
    }

    public void ReportCarSpawn(string lane)
    {
        if (carsPerLane.ContainsKey(lane)) carsPerLane[lane]++;
        SyncDictionaryToList();
    }

    public void ReportCarDestroyed(string lane)
    {
        if (carsPerLane.ContainsKey(lane)) carsPerLane[lane]--;
        SyncDictionaryToList();
    }

    public void ReportCar(float waitTime, string lane, float avgSpeed)
    {
        totalWaitTime += waitTime;
        carsReported++;
        lastReportTime = Time.time;

        totalIntersectionSpeed += avgSpeed;
        speedSamples++;

        AddRecentValue(recentWaitTimes, waitTime);
        UpdateRecentWaitAvg();
        agent.RewardCarPassed(waitTime, avgSpeed);

        AddRecentValue(recentSpeeds, avgSpeed);
        UpdateRecentSpeedAvg();
    }

    public int GetCarsOnLane(string lane)
    {
        return carsPerLane.ContainsKey(lane) ? carsPerLane[lane] : 0;
    }

    private void AddRecentValue(Queue<float> queue, float value)
    {
        if (queue.Count >= maxRecent)
            queue.Dequeue();

        queue.Enqueue(value);
    }

    private void UpdateRecentWaitAvg()
    {
        float sum = 0f;
        foreach (float w in recentWaitTimes) sum += w;
        recentAvgWaitTime = recentWaitTimes.Count > 0 ? sum / recentWaitTimes.Count : 0f;
    }

    private void UpdateRecentSpeedAvg()
    {
        float sum = 0f;
        foreach (float s in recentSpeeds) sum += s;
        recentAvgSpeed = recentSpeeds.Count > 0 ? sum / recentSpeeds.Count : 0f;
    }

    private void Update()
    {
        avrageWaitTime = carsReported > 0 ? totalWaitTime / carsReported : 0f;
        avgCarSpeed = speedSamples > 0 ? totalIntersectionSpeed / speedSamples : 0f;
        Time.timeScale = simulationTimeScale;
    }

    public (float totalUnfinishedWait, float totalUnfinishedSpeed, int unfinishedCarCount) ReportUnfinishedCars()
    {
        float totalWait = 0f;
        float totalSpeed = 0f;
        int count = 0;

        // Only find cars in the same scene as this logger
        GameObject[] allCars = GameObject.FindGameObjectsWithTag("Car");
        foreach (GameObject car in allCars)
        {
            // Check if car is in the same scene as this logger
            if (car.scene == gameObject.scene)
            {
                CarController controller = car.GetComponent<CarController>();
                if (controller != null)
                {
                    float timeSinceSpawn = Time.time - controller.spawnTime;
                    float currentSpeed = controller.currentSpeed;

                    totalWait += timeSinceSpawn;
                    totalSpeed += currentSpeed;
                    ReportCar(timeSinceSpawn, controller.laneID, currentSpeed);
                    count++;
                }
            }
        }

        return (totalWait, totalSpeed, count);
    }

    public void ReportAccident(string lane)
    {
        agent.AddReward(-1.2f);
        Debug.Log($"[ACCIDENT] Detected in lane {lane} in scene {gameObject.scene.name}");
        ReportCarDestroyed(lane);
    }

    public void ResetAllData()
    {
        // Reset wait time and counters
        totalWaitTime = 0f;
        avrageWaitTime = 0f;
        carsReported = 0;

        // Reset speed data
        totalIntersectionSpeed = 0f;
        avgCarSpeed = 0f;
        speedSamples = 0;

        // Clear recent queues
        recentWaitTimes.Clear();
        recentSpeeds.Clear();
        recentAvgWaitTime = 0f;
        recentAvgSpeed = 0f;

        // Reset car counts per lane (dictionary)
        var keys = new List<string>(carsPerLane.Keys);
        foreach (var key in keys)
        {
            carsPerLane[key] = 0;
        }

        // Reset car counts per lane (list)
        foreach (var lane in carsPerLaneList)
        {
            lane.carCount = 0;
        }
    }
}

[System.Serializable]
public class LaneCount
{
    public string laneID;
    public int carCount;
}