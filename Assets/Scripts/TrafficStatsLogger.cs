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

    [SerializeField] private float accidentPenalty;
    private float lastReportTime = 0f;
    public float TimeSinceLastReport => Time.time - lastReportTime;

    [SerializeField] public int carsReported = 0;
    [SerializeField] public int carsInIntersection = 0; // New field to track cars in intersection
    public int currentCarCount = 0;

    [Header("Anger Statistics")]
    [SerializeField] public float totalAngerScore = 0f;
    [SerializeField] public float avgAngerScore = 0f;
    [SerializeField] public float recentAvgAngerScore = 0f;
    [SerializeField] public float peakAngerScore = 0f;
    [SerializeField] public int angerReports = 0;

    [SerializeField]
    public List<LaneCount> carsPerLaneList = new List<LaneCount>()
    {
        new LaneCount { laneID = "N", carCount = 0 },
        new LaneCount { laneID = "S", carCount = 0 },
        new LaneCount { laneID = "E", carCount = 0 },
        new LaneCount { laneID = "V", carCount = 0 }
    };

    [SerializeField]
    public List<LaneAngerStats> angerPerLaneList = new List<LaneAngerStats>()
    {
        new LaneAngerStats { laneID = "N", totalAnger = 0f, avgAnger = 0f, peakAnger = 0f, angerReports = 0 },
        new LaneAngerStats { laneID = "S", totalAnger = 0f, avgAnger = 0f, peakAnger = 0f, angerReports = 0 },
        new LaneAngerStats { laneID = "E", totalAnger = 0f, avgAnger = 0f, peakAnger = 0f, angerReports = 0 },
        new LaneAngerStats { laneID = "V", totalAnger = 0f, avgAnger = 0f, peakAnger = 0f, angerReports = 0 }
    };

    private Dictionary<string, int> carsPerLane = new Dictionary<string, int>();
    private Dictionary<string, LaneAngerStats> angerPerLane = new Dictionary<string, LaneAngerStats>();
    private float totalIntersectionSpeed = 0f;
    private int speedSamples = 0;

    // Queues to store last 5 values
    private Queue<float> recentWaitTimes = new Queue<float>();
    private Queue<float> recentSpeeds = new Queue<float>();
    private Queue<float> recentAngerScores = new Queue<float>();
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

        // Sync anger stats list to dictionary
        foreach (var angerStats in angerPerLaneList)
        {
            angerPerLane[angerStats.laneID] = angerStats;
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

    void SyncAngerDictionaryToList()
    {
        foreach (var angerStats in angerPerLaneList)
        {
            if (angerPerLane.ContainsKey(angerStats.laneID))
            {
                var stats = angerPerLane[angerStats.laneID];
                angerStats.totalAnger = stats.totalAnger;
                angerStats.avgAnger = stats.avgAnger;
                angerStats.peakAnger = stats.peakAnger;
                angerStats.angerReports = stats.angerReports;
            }
        }
    }

    public void ReportCarSpawn(string lane)
    {
        if (carsPerLane.ContainsKey(lane)) carsPerLane[lane]++;
        SyncDictionaryToList();
        currentCarCount++;
    }

    public void ReportCarDestroyed(string lane)
    {
        if (carsPerLane.ContainsKey(lane)) carsPerLane[lane]--;
        SyncDictionaryToList();
        currentCarCount--;
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

    // New function to report anger data
    public void ReportAngerData(string laneID, float angerScore, float totalWaitTime)
    {
        // Update global anger statistics
        totalAngerScore += angerScore;
        angerReports++;
        
        // Update peak anger
        if (angerScore > peakAngerScore)
        {
            peakAngerScore = angerScore;
        }

        // Update lane-specific anger statistics
        if (angerPerLane.ContainsKey(laneID))
        {
            var laneStats = angerPerLane[laneID];
            laneStats.totalAnger += angerScore;
            laneStats.angerReports++;
            
            // Update lane peak anger
            if (angerScore > laneStats.peakAnger)
            {
                laneStats.peakAnger = angerScore;
            }
            
            // Update lane average anger
            laneStats.avgAnger = laneStats.totalAnger / laneStats.angerReports;
        }

        // Add to recent anger scores for running average
        AddRecentValue(recentAngerScores, angerScore);
        UpdateRecentAngerAvg();

        // Sync to inspector-visible list
        SyncAngerDictionaryToList();

        // Optional: Log anger data for debugging
        // Debug.Log($"[ANGER] Lane {laneID}: Anger={angerScore:F2}, WaitTime={totalWaitTime:F2}s");
        
        // Provide feedback to ML agent based on anger levels
        if (agent != null)
        {
            // Higher anger = negative reward
            float angerPenalty = -angerScore * 0.1f; // Scale as needed
            agent.AddReward(angerPenalty);
        }
    }

    // New function to track cars entering the intersection
    public void ReportCarEnterIntersection(string lane)
    {
        carsInIntersection++;
        //Debug.Log($"[INTERSECTION] Car entered intersection from lane {lane}. Total cars in intersection: {carsInIntersection}");
    }

    // New function to track cars exiting the intersection
    public void ReportCarExitIntersection(string lane)
    {
        carsInIntersection = Mathf.Max(0, carsInIntersection - 1); // Ensure it doesn't go below 0
        //Debug.Log($"[INTERSECTION] Car exited intersection to lane {lane}. Total cars in intersection: {carsInIntersection}");
    }

    // New function to get the current number of cars in intersection
    public int GetCarsInIntersection()
    {
        return carsInIntersection;
    }

    public int GetCarsOnLane(string lane)
    {
        return carsPerLane.ContainsKey(lane) ? carsPerLane[lane] : 0;
    }

    // New function to get anger statistics for a specific lane
    public LaneAngerStats GetLaneAngerStats(string laneID)
    {
        return angerPerLane.ContainsKey(laneID) ? angerPerLane[laneID] : null;
    }

    // New function to get overall anger statistics
    public (float avgAnger, float peakAnger, float recentAvgAnger, int totalReports) GetOverallAngerStats()
    {
        float avgAnger = angerReports > 0 ? totalAngerScore / angerReports : 0f;
        return (avgAnger, peakAngerScore, recentAvgAngerScore, angerReports);
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

    private void UpdateRecentAngerAvg()
    {
        float sum = 0f;
        foreach (float a in recentAngerScores) sum += a;
        recentAvgAngerScore = recentAngerScores.Count > 0 ? sum / recentAngerScores.Count : 0f;
    }

    private void Update()
    {
        avrageWaitTime = carsReported > 0 ? totalWaitTime / carsReported : 0f;
        avgCarSpeed = speedSamples > 0 ? totalIntersectionSpeed / speedSamples : 0f;
        avgAngerScore = angerReports > 0 ? totalAngerScore / angerReports : 0f;
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
                    
                    // Also report final anger data for unfinished cars
                    float finalAngerScore = controller.GetAngerScore();
                    float finalWaitTime = controller.GetTotalWaitTime();
                    ReportAngerData(controller.laneID, finalAngerScore, finalWaitTime);
                    
                    count++;
                }
            }
        }

        return (totalWait, totalSpeed, count);
    }

    public void ReportAccident(string lane)
    {
        agent.AddReward(-1f * accidentPenalty);
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

        // Reset intersection counter
        carsInIntersection = 0;

        // Reset anger data
        totalAngerScore = 0f;
        avgAngerScore = 0f;
        recentAvgAngerScore = 0f;
        peakAngerScore = 0f;
        angerReports = 0;

        // Clear recent queues
        recentWaitTimes.Clear();
        recentSpeeds.Clear();
        recentAngerScores.Clear();
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

        // Reset anger stats per lane (dictionary)
        var angerKeys = new List<string>(angerPerLane.Keys);
        foreach (var key in angerKeys)
        {
            var stats = angerPerLane[key];
            stats.totalAnger = 0f;
            stats.avgAnger = 0f;
            stats.peakAnger = 0f;
            stats.angerReports = 0;
        }

        // Reset anger stats per lane (list)
        foreach (var angerStats in angerPerLaneList)
        {
            angerStats.totalAnger = 0f;
            angerStats.avgAnger = 0f;
            angerStats.peakAnger = 0f;
            angerStats.angerReports = 0;
        }
    }
}

[System.Serializable]
public class LaneCount
{
    public string laneID;
    public int carCount;
}

[System.Serializable]
public class LaneAngerStats
{
    public string laneID;
    public float totalAnger;
    public float avgAnger;
    public float peakAnger;
    public int angerReports;
}