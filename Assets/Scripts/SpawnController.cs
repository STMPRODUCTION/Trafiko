using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using Unity.MLAgents;

public class CarSpawner : MonoBehaviour
{
    [Header("Car Prefabs")]
    public List<GameObject> carPrefabs = new List<GameObject>();

    [Header("Spawn Points")]
    public Transform spawnN;
    public Transform spawnS;
    public Transform spawnE;
    public Transform spawnV;

    [Header("Traffic Lights")]
    public TrafficLight lightN;
    public TrafficLight lightS;
    public TrafficLight lightE;
    public TrafficLight lightV;

    [Header("Basic Spawn Settings")]
    [Tooltip("Base spawn interval in seconds during normal hours")]
    public float baseSpawnInterval = 3f;
    
    [Tooltip("Minimum spawn interval (prevents too rapid spawning)")]
    public float minSpawnInterval = 0.5f;
    
    [Tooltip("Maximum spawn interval")]
    public float maxSpawnInterval = 8f;

    [Header("Episode and Time Settings")]
    [Tooltip("Total length of one episode in seconds (e.g., 300 = 5 minutes)")]
    public float maxEpisodeLength = 300f;
    
    [Tooltip("Time multiplier - how fast time passes (1 = real time, 2 = 2x speed)")]
    public float timeMultiplier = 10f;

    [Header("Rush Hour Configuration")]
    [Tooltip("Morning rush hour start (0-1, where 0=start of episode, 1=end)")]
    [Range(0f, 1f)]
    public float morningRushStart = 0.15f; // ~15% into episode
    
    [Tooltip("Morning rush hour end")]
    [Range(0f, 1f)]
    public float morningRushEnd = 0.35f; // ~35% into episode
    
    [Tooltip("Afternoon rush hour start")]
    [Range(0f, 1f)]
    public float afternoonRushStart = 0.65f; // ~65% into episode
    
    [Tooltip("Afternoon rush hour end")]
    [Range(0f, 1f)]
    public float afternoonRushEnd = 0.85f; // ~85% into episode
    
    [Tooltip("Rush hour spawn rate multiplier")]
    [Range(1f, 5f)]
    public float rushHourMultiplier = 3f;

    [Header("Wave Spawning")]
    [Tooltip("Enable wave-based spawning during rush hours")]
    public bool enableWaveSpawning = true;
    
    [Tooltip("How many cars spawn in each wave")]
    [Range(10, 30)]
    public int carsPerWave = 8;
    
    [Tooltip("Time between each car in a wave (seconds)")]
    [Range(0.05f, 2f)]
    public float carSpawnDelay = 0.3f;
    
    [Tooltip("Gap between waves in seconds")]
    [Range(2f,12f)]
    public float waveGapDuration = 5f;

    [Header("Lane Bias (Residential Area Simulation)")]
    [Tooltip("Which lane represents the residential area (0=N, 1=S, 2=E, 3=V)")]
    public int residentialLaneIndex = 0;
    
    [Tooltip("How much more traffic comes from residential during morning rush")]
    [Range(1f, 5f)]
    public float morningResidentialBias = 3f;
    
    [Tooltip("How much more traffic goes to residential during afternoon rush")]
    [Range(1f, 5f)]
    public float afternoonResidentialBias = 2.5f;

    [Header("Queue-based Spawn Settings")]
    [Tooltip("Minimum distance from spawn point to check for cars")]
    public float minSpawnDistance = 5f;
    
    [Tooltip("How far back behind spawn point to search for free space")]
    public float maxSearchDistance = 30f;
    
    [Tooltip("Distance between cars when queuing")]
    public float carSpacing = 4f;
    
    [Tooltip("Maximum number of cars allowed to queue behind spawn point")]
    public int maxQueueLength = 8;
    
    public LayerMask carLayerMask = -1;

    [Header("Performance Settings")]
    public int maxCars = 100;
    public float carCountCheckInterval = 1f;
    private float carCountTimer;
    private int currentCarCount = 0;

    [SerializeField] private TrafficStatsLogger statsLogger;

    // Time tracking
    private float episodeTime = 0f;
    private float normalizedTime = 0f; // 0-1 through the episode

    // Wave system
    private bool isInWave = false;
    private float waveTimer = 0f;
    private bool waveSystemActive = false;
    private int carsSpawnedInCurrentWave = 0;
    private int currentWaveLane = 0;
    private float carSpawnTimer = 0f;

    // Spawn timing
    private float spawnTimer;
    private float currentSpawnInterval;
    
    // Traffic state
    private enum TrafficPeriod
    {
        EarlyMorning,
        MorningRush,
        Midday,
        AfternoonRush,
        Evening
    }
    private TrafficPeriod currentTrafficPeriod = TrafficPeriod.EarlyMorning;

    // Cache spawn data
    private SpawnData[] spawnPoints;
    private readonly Collider[] overlapBuffer = new Collider[20]; // Increased buffer size

    [System.Serializable]
    private struct SpawnData
    {
        public Transform spawnPoint;
        public TrafficLight light;
        public string laneID;
    }

    void Start()
    {
        carCountTimer = carCountCheckInterval;
        residentialLaneIndex = Random.Range(0,5);
        
        // Cache spawn data for better performance
        spawnPoints = new SpawnData[4]
        {
            new SpawnData { spawnPoint = spawnN, light = lightN, laneID = "N" },
            new SpawnData { spawnPoint = spawnS, light = lightS, laneID = "S" },
            new SpawnData { spawnPoint = spawnE, light = lightE, laneID = "E" },
            new SpawnData { spawnPoint = spawnV, light = lightV, laneID = "V" }
        };

        OnEpisodeBegin();
        //Debug.Log($"CarSpawner initialized with queue-based spawning system");
    }

    public void OnEpisodeBegin()
    {
        episodeTime = 0f;
        normalizedTime = 0f;
        currentTrafficPeriod = TrafficPeriod.EarlyMorning;
        
        // Reset wave system
        isInWave = false;
        waveTimer = 0f;
        waveSystemActive = false;
        carsSpawnedInCurrentWave = 0;
        carSpawnTimer = 0f;
        
        UpdateTrafficPeriodAndSpawnRate();
        spawnTimer = Random.Range(0f, currentSpawnInterval);
        
        Debug.Log($"Episode began - Starting with {currentTrafficPeriod} traffic pattern");
    }

    void Update()
    {
        currentCarCount = statsLogger.currentCarCount;
        // Update episode time
        episodeTime += Time.deltaTime * timeMultiplier;
        normalizedTime = Mathf.Clamp01(episodeTime / maxEpisodeLength);

        // Update traffic period and spawn rates
        UpdateTrafficPeriodAndSpawnRate();

        // Handle wave spawning during rush hours
        if (waveSystemActive && enableWaveSpawning)
        {
            UpdateWaveSystem();
        }
        else
        {
            // Normal spawning outside rush hours or when waves disabled
            spawnTimer += Time.deltaTime;
            if (spawnTimer >= currentSpawnInterval)
            {
                spawnTimer = 0f;
                SpawnCarsBasedOnTrafficPeriod();
            }
        }
    }

    void UpdateTrafficPeriodAndSpawnRate()
    {
        TrafficPeriod newPeriod = GetCurrentTrafficPeriod();
        
        if (newPeriod != currentTrafficPeriod)
        {
            currentTrafficPeriod = newPeriod;
            //Debug.Log($"Traffic period changed to: {currentTrafficPeriod} at time {episodeTime:F1}s ({normalizedTime:P0})");
        }

        // Update spawn rate based on current period
        float baseRate = baseSpawnInterval;
        
        switch (currentTrafficPeriod)
        {
            case TrafficPeriod.MorningRush:
            case TrafficPeriod.AfternoonRush:
                baseRate = baseSpawnInterval / rushHourMultiplier;
                waveSystemActive = enableWaveSpawning;
                break;
            case TrafficPeriod.EarlyMorning:
            case TrafficPeriod.Evening:
                baseRate = baseSpawnInterval * 1.5f; // Slower during off-hours
                waveSystemActive = false;
                break;
            case TrafficPeriod.Midday:
                baseRate = baseSpawnInterval; // Normal rate
                waveSystemActive = false;
                break;
        }

        // Apply wave multiplier if in wave
        if (isInWave && waveSystemActive)
        {
            // During waves, we don't use the normal spawn interval
            // Instead we use the wave system timing
        }
        else
        {
            currentSpawnInterval = Mathf.Clamp(baseRate, minSpawnInterval, maxSpawnInterval);
        }
    }

    TrafficPeriod GetCurrentTrafficPeriod()
    {
        if (normalizedTime >= morningRushStart && normalizedTime <= morningRushEnd)
            return TrafficPeriod.MorningRush;
        else if (normalizedTime >= afternoonRushStart && normalizedTime <= afternoonRushEnd)
            return TrafficPeriod.AfternoonRush;
        else if (normalizedTime < morningRushStart)
            return TrafficPeriod.EarlyMorning;
        else if (normalizedTime > morningRushEnd && normalizedTime < afternoonRushStart)
            return TrafficPeriod.Midday;
        else
            return TrafficPeriod.Evening;
    }

    void UpdateWaveSystem()
    {
        if (!isInWave)
        {
            // Waiting for next wave
            waveTimer += Time.deltaTime;
            if (waveTimer >= waveGapDuration)
            {
                // Start new wave
                StartNewWave();
            }
        }
        else
        {
            // Currently spawning a wave of cars
            carSpawnTimer += Time.deltaTime;
            
            if (carSpawnTimer >= carSpawnDelay && carsSpawnedInCurrentWave < carsPerWave)
            {
                // Spawn next car in the wave from the same lane
                if (currentCarCount < maxCars && CanSpawnInLane(currentWaveLane))
                {
                    SpawnCarAtLane(currentWaveLane);
                    carsSpawnedInCurrentWave++;
                    carSpawnTimer = 0f;
                }
            }
            
            // Check if wave is complete
            if (carsSpawnedInCurrentWave >= carsPerWave)
            {
                EndCurrentWave();
            }
        }
    }

    void StartNewWave()
    {
        isInWave = true;
        waveTimer = 0f;
        carsSpawnedInCurrentWave = 0;
        carSpawnTimer = 0f;
        
        // Choose which lane will produce this wave based on traffic period
        currentWaveLane = ChooseWaveLane();
        
        //Debug.Log($"Traffic wave started from lane {spawnPoints[currentWaveLane].laneID} during {currentTrafficPeriod} - spawning {carsPerWave} cars");
    }
    
    void EndCurrentWave()
    {
        isInWave = false;
        waveTimer = 0f;
        //Debug.Log($"Traffic wave completed from lane {spawnPoints[currentWaveLane].laneID} - spawned {carsSpawnedInCurrentWave} cars");
    }
    
    int ChooseWaveLane()
    {
        switch (currentTrafficPeriod)
        {
            case TrafficPeriod.MorningRush:
                // 70% chance from residential area, 30% from others
                if (Random.Range(0f, 1f) < 0.7f)
                    return residentialLaneIndex;
                else
                    return Random.Range(0, 4);
                    
            case TrafficPeriod.AfternoonRush:
                // 70% chance from non-residential lanes (people coming home)
                float rand = Random.Range(0f, 1f);
                if (rand < 0.7f)
                {
                    // Pick a random non-residential lane
                    List<int> nonResidentialLanes = new List<int>();
                    for (int i = 0; i < 4; i++)
                    {
                        if (i != residentialLaneIndex)
                            nonResidentialLanes.Add(i);
                    }
                    return nonResidentialLanes[Random.Range(0, nonResidentialLanes.Count)];
                }
                else
                {
                    return residentialLaneIndex;
                }
                    
            default:
                // Random lane for non-rush hours
                return Random.Range(0, 4);
        }
    }

    void SpawnCarsBasedOnTrafficPeriod()
    {
        switch (currentTrafficPeriod)
        {
            case TrafficPeriod.MorningRush:
                SpawnMorningRushTraffic();
                break;
            case TrafficPeriod.AfternoonRush:
                SpawnAfternoonRushTraffic();
                break;
            default:
                SpawnNormalTraffic();
                break;
        }
    }

    void SpawnMorningRushTraffic()
    {
        // More traffic FROM residential area (people going to work)
        float[] laneProbabilities = { 1f, 1f, 1f, 1f };
        laneProbabilities[residentialLaneIndex] *= morningResidentialBias;

        int selectedLane = GetWeightedRandomLane(laneProbabilities);
        SpawnCarAtLane(selectedLane);
    }

    void SpawnAfternoonRushTraffic()
    {
        // More traffic TO residential area (people coming home from work)
        // This means more traffic from OTHER lanes going towards residential
        float[] laneProbabilities = { 1f, 1f, 1f, 1f };
        
        // Increase probability for all lanes except residential
        for (int i = 0; i < laneProbabilities.Length; i++)
        {
            if (i != residentialLaneIndex)
            {
                laneProbabilities[i] *= afternoonResidentialBias;
            }
        }

        int selectedLane = GetWeightedRandomLane(laneProbabilities);
        SpawnCarAtLane(selectedLane);
    }

    void SpawnNormalTraffic()
    {
        // Equal probability for all lanes
        int direction = Random.Range(0, 4);
        SpawnCarAtLane(direction);
    }

    int GetWeightedRandomLane(float[] weights)
    {
        float totalWeight = 0f;
        for (int i = 0; i < weights.Length; i++)
        {
            totalWeight += weights[i];
        }

        float randomValue = Random.Range(0f, totalWeight);
        float currentWeight = 0f;

        for (int i = 0; i < weights.Length; i++)
        {
            currentWeight += weights[i];
            if (randomValue <= currentWeight)
            {
                return i;
            }
        }

        return 0; // Fallback
    }
    /// <summary>
    /// Finds the best spawn position for a car in the given lane, checking backwards from spawn point
    /// </summary>
    Vector3 FindBestSpawnPosition(Transform spawnPoint)
    {
        // Check original spawn position first
        if (IsPositionClear(spawnPoint.position))
        {
            return spawnPoint.position;
        }

        // If spawn point is blocked, search backwards
        Vector3 searchDirection = -spawnPoint.forward; // Backwards from spawn direction
        
        for (int i = 1; i <= maxQueueLength; i++)
        {
            float searchDistance = i * carSpacing;
            Vector3 testPosition = spawnPoint.position + (searchDirection * searchDistance);
            
            if (IsPositionClear(testPosition))
            {
                return testPosition;
            }
        }
        
        // No suitable position found within max queue length
        return Vector3.zero; // Invalid position indicator
    }

    /// <summary>
    /// Checks if a position is clear for spawning a car
    /// </summary>
    bool IsPositionClear(Vector3 position)
    {
        int hitCount = Physics.OverlapSphereNonAlloc(
            position, 
            minSpawnDistance, 
            overlapBuffer, 
            carLayerMask
        );
        
        for (int i = 0; i < hitCount; i++)
        {
            if (overlapBuffer[i] != null && overlapBuffer[i].CompareTag("Car"))
            {
                return false;
            }
        }
        
        return true;
    }

    /// <summary>
    /// Legacy method - now uses queue-based spawning
    /// </summary>
    bool IsSpawnPointClear(Transform spawnPoint)
    {
        return IsPositionClear(spawnPoint.position);
    }

    GameObject GetRandomCarPrefab()
    {
        if (carPrefabs.Count == 0)
        {
            Debug.LogError("No car prefabs assigned to CarSpawner!");
            return null;
        }
        
        int randomIndex = Random.Range(0, carPrefabs.Count);
        return carPrefabs[randomIndex];
    }

    void SpawnCarAtLane(int laneIndex)
    {
        if (laneIndex < 0 || laneIndex >= spawnPoints.Length) return;
        if (currentCarCount >= maxCars) return;
        
        SpawnData spawnData = spawnPoints[laneIndex];
        
        // Find the best spawn position (could be behind spawn point if it's blocked)
        Vector3 spawnPosition = FindBestSpawnPosition(spawnData.spawnPoint);
        
        // If no valid position found, skip spawning
        if (spawnPosition == Vector3.zero)
        {
            Debug.Log($"Could not find valid spawn position in lane {spawnData.laneID} - queue too long");
            return;
        }

        GameObject selectedPrefab = GetRandomCarPrefab();
        if (selectedPrefab == null) return;

        GameObject car = Instantiate(selectedPrefab, spawnPosition, spawnData.spawnPoint.rotation);
        SceneManager.MoveGameObjectToScene(car, gameObject.scene);
        car.tag = "Car";
        
        CarController carCtrl = car.GetComponent<CarController>();
        if (carCtrl != null)
        {
            carCtrl.statsLogger = statsLogger;
            carCtrl.targetLight = spawnData.light;
            carCtrl.laneID = spawnData.laneID;
        }
        
        if (statsLogger != null)
        {
            statsLogger.ReportCarSpawn(spawnData.laneID);
        }
        
        // Log if car was spawned behind spawn point
        float distanceFromSpawn = Vector3.Distance(spawnPosition, spawnData.spawnPoint.position);
        if (distanceFromSpawn > 0.1f)
        {
            //Debug.Log($"Car spawned {distanceFromSpawn:F1}m behind spawn point in lane {spawnData.laneID}");
        }
    }

    /// <summary>
    /// Checks if we can spawn in the given lane (either at spawn point or in queue behind it)
    /// </summary>
    public bool CanSpawnInLane(int laneIndex)
    {
        if (laneIndex < 0 || laneIndex >= spawnPoints.Length) return false;
        if (currentCarCount >= maxCars) return false;
        
        SpawnData spawnData = spawnPoints[laneIndex];
        Vector3 spawnPosition = FindBestSpawnPosition(spawnData.spawnPoint);
        
        return spawnPosition != Vector3.zero;
    }

    /// <summary>
    /// Legacy method name for compatibility
    /// </summary>
    public bool canSpawn(int laneIndex)
    {
        return CanSpawnInLane(laneIndex);
    }

    // Public methods for external control
    public float GetCurrentEpisodeTime() => episodeTime;
    public float GetNormalizedTime() => normalizedTime;
    public bool IsInWave() => isInWave;
    public float GetCurrentSpawnRate() => currentSpawnInterval;

    // Debug visualization
    void OnDrawGizmosSelected()
    {
        if (Application.isPlaying)
        {
            #if UNITY_EDITOR
            Vector3 labelPos = transform.position + Vector3.up * 2f;
            UnityEditor.Handles.Label(labelPos, 
                $"Time: {episodeTime:F1}s ({normalizedTime:P0})\n" +
                $"Spawn Rate: {currentSpawnInterval:F2}s\n" +
                $"Wave: {(isInWave ? "Active" : "Inactive")}\n" +
                $"Cars: {currentCarCount}/{maxCars}");
            #endif
        }

        // Draw spawn points and queue areas
        if (spawnPoints != null)
        {
            Gizmos.color = Color.green;
            foreach (var spawnData in spawnPoints)
            {
                if (spawnData.spawnPoint != null)
                {
                    // Draw spawn point
                    Gizmos.DrawWireSphere(spawnData.spawnPoint.position, minSpawnDistance);
                    
                    // Draw queue area
                    Vector3 queueStart = spawnData.spawnPoint.position;
                    Vector3 queueEnd = queueStart - (spawnData.spawnPoint.forward * (maxQueueLength * carSpacing));
                    
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawLine(queueStart, queueEnd);
                    
                    // Draw potential spawn positions in queue
                    for (int i = 1; i <= maxQueueLength; i++)
                    {
                        Vector3 queuePos = queueStart - (spawnData.spawnPoint.forward * (i * carSpacing));
                        Gizmos.DrawWireCube(queuePos, Vector3.one * 2f);
                    }
                    
                    Gizmos.color = Color.green;
                }
            }
        }
    }
}