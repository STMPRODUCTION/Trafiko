using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using UnityEngine;

public class TrafficLightAgent : Agent
{
    [Header("Straight Lane Traffic Lights")]
    public TrafficLight lightN;
    public TrafficLight lightS;
    public TrafficLight lightE;
    public TrafficLight lightV;

    [Header("Left Turn Lane Traffic Lights")]
    public TrafficLight lightNLeft;
    public TrafficLight lightSLeft;
    public TrafficLight lightELeft;
    public TrafficLight lightVLeft;

    public float decisionInterval = 5f;
    private float timer = 0f;
    [SerializeField] private float maxEpisodeTime = 120f;
    [SerializeField] private float episodeTimer = 0f;

    // 🔧 FIXED: Balanced reward weights
    [SerializeField] private float speedRewardWeight = 1.0f;
    [SerializeField] private float waitPenaltyWeight = 0.5f;
    [SerializeField] private float conflictPenaltyWeight = 2.0f;
    [SerializeField] private float congestionPenaltyWeight = 0.1f;
    [SerializeField] private float carPassedRewardWeight = 0.5f;
    [SerializeField] private float unfinishedCarPenaltyWeight = 0.1f;
    [SerializeField] private float maxInactivityTime = 10f;
    [SerializeField] private float NoAccidentReward = 10f;
    
    // 🔧 NEW: Anger-based reward weights
    [SerializeField] private float angerPenaltyWeight = 0.3f;
    [SerializeField] private float peakAngerPenaltyWeight = 0.5f;
    [SerializeField] private float angerReductionRewardWeight = 0.2f;
    [SerializeField] private float inactivityPenaltyWeight = 0.2f;
    
    
    // 🔧 UPDATED: Track previous state for all 8 lanes to prevent oscillation
    private bool[] previousLightStates = new bool[8]; // 4 straight + 4 left turn

    [SerializeField] private float stabilityRewardWeight = 0.1f;
    
    // 🔧 UPDATED: Track previous anger levels for all 8 lanes
    private float previousOverallAnger = 0f;
    private float[] previousLaneAnger = new float[8]; // 4 straight + 4 left turn
    
    // 🔧 NEW: Cache the scene-specific logger
    [SerializeField] private TrafficStatsLogger statsLogger;
    [SerializeField] private CarSpawner carSpawner;

    // 🔧 NEW: Random episode start configuration
    [Header("Episode Start Configuration")]
    [SerializeField] private bool randomizeInitialLights = true;
    [SerializeField] private float randomGreenProbability = 0.3f; // 30% chance for each light to be green
    [SerializeField] private bool preventInitialConflicts = true; // Prevent conflicting directions from being green initially

    public override void OnEpisodeBegin()
    {
        carSpawner.OnEpisodeBegin();
        // Ensure we have the logger reference
        if (statsLogger == null)
        {
            statsLogger = TrafficStatsLogger.GetInstanceForGameObject(gameObject);
        }

        // 🔧 UPDATED: Initialize predefined configurations
        InitializeTrafficConfigurations();

        // 🔧 UPDATED: Randomize initial traffic light states using configurations
        if (randomizeInitialLights)
        {
            SetRandomInitialConfiguration();
        }
        else
        {
            // Original behavior: all lights off (configuration 0)
            SetTrafficConfiguration(0);
        }
        
        // 🔧 UPDATED: Reset tracking for 8 lanes
        episodeTimer = 0f;
        timer = 0f;
        previousLightStates = new bool[8];
        previousOverallAnger = 0f;
        previousLaneAnger = new float[8];
        currentConfigurationIndex = 0;
        
        // Only destroy cars in the same scene
        GameObject[] allCars = GameObject.FindGameObjectsWithTag("Car");
        foreach (GameObject car in allCars)
        {
            if (car.scene == gameObject.scene)
            {
                Destroy(car);
            }
        }
        
        // Reset logger data
        if (statsLogger != null)
        {
            statsLogger.ResetAllData();
        }
    }

    // 🔧 NEW: Define all safe traffic configurations
    private bool[][] trafficConfigurations;
    private int currentConfigurationIndex = 0;

    private void InitializeTrafficConfigurations()
    {
        // Predefined safe configurations for 8 lanes (straight + left turn)
        // Format: [N, S, E, V, NLeft, SLeft, ELeft, VLeft]
        trafficConfigurations = new bool[][]
        {
            // Configuration 0: All red
            new bool[] { false, false, false, false, false, false, false, false },
            // Configuration 1-2: Straight lane pairs (non-conflicting)
            new bool[] { true, true, false, false, false, false, false, false },   // North-South straight
            new bool[] { false, false, true, true, false, false, false, false },   // East-West straight
            
            // Configuration 3-6: Mixed safe configurations (straight + same direction left)
            new bool[] { true, false, false, false, true, false, false, false },   // North straight + North left
            new bool[] { false, true, false, false, false, true, false, false },   // South straight + South left
            new bool[] { false, false, true, false, false, false, true, false },   // East straight + East left
            new bool[] { false, false, false, true, false, false, false, true },   // West straight + West left
        };
    }

    // 🔧 NEW: Set traffic lights based on configuration index
    private void SetTrafficConfiguration(int configIndex)
    {
        if (configIndex < 0 || configIndex >= trafficConfigurations.Length)
        {
            Debug.LogWarning($"Invalid configuration index: {configIndex}. Using configuration 0 (all red).");
            configIndex = 0;
        }
        
        currentConfigurationIndex = configIndex;
        bool[] config = trafficConfigurations[configIndex];
        
        SetAllLights(config[0], config[1], config[2], config[3], 
                    config[4], config[5], config[6], config[7]);
    }

    // 🔧 UPDATED: Helper method to set all lights at once
    private void SetAllLights(bool n, bool s, bool e, bool v, bool nLeft, bool sLeft, bool eLeft, bool vLeft)
    {
        lightN.SetGreen(n);
        lightS.SetGreen(s);
        lightE.SetGreen(e);
        lightV.SetGreen(v);
        lightNLeft.SetGreen(nLeft);
        lightSLeft.SetGreen(sLeft);
        lightELeft.SetGreen(eLeft);
        lightVLeft.SetGreen(vLeft);
    }

    // 🔧 UPDATED: Method to set random initial configuration
    private void SetRandomInitialConfiguration()
    {
        int randomConfigIndex = Random.Range(0, trafficConfigurations.Length);
        SetTrafficConfiguration(randomConfigIndex);
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (statsLogger == null)
        {
            // Fallback: Add zero observations if logger is missing
            // Lane car counts (straight lanes)
            sensor.AddObservation(0f); // N lane cars
            sensor.AddObservation(0f); // S lane cars
            sensor.AddObservation(0f); // E lane cars
            sensor.AddObservation(0f); // V lane cars
            
            // Left turn lane car counts
            sensor.AddObservation(0f); // N left lane cars
            sensor.AddObservation(0f); // S left lane cars
            sensor.AddObservation(0f); // E left lane cars
            sensor.AddObservation(0f); // V left lane cars
            
            // Traffic metrics
            sensor.AddObservation(0f); // recent avg wait time
            sensor.AddObservation(0f); // recent avg speed
            sensor.AddObservation(0f); // cars on intersection
            
            // Add zero anger observations
            sensor.AddObservation(0f); // overall anger
            sensor.AddObservation(0f); // peak anger
            sensor.AddObservation(0f); // recent avg anger
            
            // Anger for all 8 lanes
            for (int i = 0; i < 8; i++)
            {
                sensor.AddObservation(0f); // Lane anger
            }
        }
        else
        {
            // Straight lane observations
            sensor.AddObservation(statsLogger.GetCarsOnLane("N"));
            sensor.AddObservation(statsLogger.GetCarsOnLane("S"));
            sensor.AddObservation(statsLogger.GetCarsOnLane("E"));
            sensor.AddObservation(statsLogger.GetCarsOnLane("V"));
            
            // Left turn lane observations
            sensor.AddObservation(statsLogger.GetCarsOnLane("NLeft"));
            sensor.AddObservation(statsLogger.GetCarsOnLane("SLeft"));
            sensor.AddObservation(statsLogger.GetCarsOnLane("ELeft"));
            sensor.AddObservation(statsLogger.GetCarsOnLane("VLeft"));
            
            sensor.AddObservation(statsLogger.GetCarsInIntersection());

            // Normalize observations
            //sensor.AddObservation(statsLogger.recentAvgWaitTime);
            //sensor.AddObservation(statsLogger.recentAvgSpeed);
            //sensor.AddObservation(statsLogger.carsReported);
            sensor.AddObservation(statsLogger.AcidentInstances);
            //sensor.AddObservation(statsLogger.totalWaitTime);
            sensor.AddObservation(statsLogger.TimeSinceLastReport);
            //sensor.AddObservation(statsLogger.totalAngerScore);
        }

        // Current light states for all 8 lanes
        sensor.AddObservation(lightN.isGreen ? 1 : 0);
        sensor.AddObservation(lightS.isGreen ? 1 : 0);
        sensor.AddObservation(lightE.isGreen ? 1 : 0);
        sensor.AddObservation(lightV.isGreen ? 1 : 0);
        sensor.AddObservation(lightNLeft.isGreen ? 1 : 0);
        sensor.AddObservation(lightSLeft.isGreen ? 1 : 0);
        sensor.AddObservation(lightELeft.isGreen ? 1 : 0);
        sensor.AddObservation(lightVLeft.isGreen ? 1 : 0);
        
        // 🔧 NEW: Add current configuration index as observation
        sensor.AddObservation((float)currentConfigurationIndex / trafficConfigurations.Length);
        
        // Time-based observations
        sensor.AddObservation(timer / decisionInterval);
        sensor.AddObservation(episodeTimer / maxEpisodeTime);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        // 🔧 UPDATED: Store previous states for all 8 lanes
        previousLightStates[0] = lightN.isGreen;
        previousLightStates[1] = lightS.isGreen;
        previousLightStates[2] = lightE.isGreen;
        previousLightStates[3] = lightV.isGreen;
        previousLightStates[4] = lightNLeft.isGreen;
        previousLightStates[5] = lightSLeft.isGreen;
        previousLightStates[6] = lightELeft.isGreen;
        previousLightStates[7] = lightVLeft.isGreen;

        // 🔧 NEW: Apply configuration-based light changes
        // The agent now chooses from predefined safe configurations
        int chosenConfigIndex = actions.DiscreteActions[0]; // Single action: choose configuration
        SetTrafficConfiguration(chosenConfigIndex);
        
        // Get current configuration for reward calculation
        bool[] currentConfig = trafficConfigurations[currentConfigurationIndex];
        bool newN = currentConfig[0];
        bool newS = currentConfig[1];
        bool newE = currentConfig[2];
        bool newV = currentConfig[3];
        bool newNLeft = currentConfig[4];
        bool newSLeft = currentConfig[5];
        bool newELeft = currentConfig[6];
        bool newVLeft = currentConfig[7];

        // --- 🔧 IMPROVED Reward Shaping with Configuration-Based Approach ---
        float reward = 0f;

        if (statsLogger != null)
        {
            // ✅ Reward for throughput (normalized)
            float normalizedSpeed = Mathf.Clamp01(statsLogger.recentAvgSpeed / 10f);
            reward += normalizedSpeed * speedRewardWeight;

            // ❌ Penalize waiting (normalized)
            float normalizedWait = Mathf.Clamp01(statsLogger.recentAvgWaitTime / 30f);
            reward -= normalizedWait * waitPenaltyWeight;

            // ❌ Congestion penalty (scaled) for all 8 lanes
            string[] allLanes = { "N", "S", "E", "V", "NLeft", "SLeft", "ELeft", "VLeft" };
            foreach (var lane in allLanes)
            {
                int count = statsLogger.GetCarsOnLane(lane);
                if (count > 5)
                {
                    reward -= congestionPenaltyWeight * (count - 5) * 0.3f;
                }
            }
        }
        if (statsLogger.carsInIntersection < 1)
        {
            reward -= angerReductionRewardWeight;
        }
        if (currentConfigurationIndex == 0) // Not the all-red configuration
        {
            reward -= 0.1f;
        }
        
        AddReward(reward);
        
        Debug.Log($"Total step reward: {reward:F3}, " + $"Cumulative: {GetCumulativeReward():F3}");
    }

    // 🔧 NEW: Enhanced conflict detection method for 8-lane intersection
    private bool CheckTrafficConflicts(bool n, bool s, bool e, bool v, bool nLeft, bool sLeft, bool eLeft , bool vLeft)
    {
        bool hasConflict = false;

        // 1. Basic straight lane conflicts (North-South vs East-West)
        if ((n || s) && (e || v))
        {
            hasConflict = true;
        }

        // 2. Left turn conflicts with opposing straight traffic
        // North left conflicts with East straight
        if (nLeft && e) hasConflict = true;
        // South left conflicts with West straight  
        if (sLeft && v) hasConflict = true;
        // East left conflicts with South straight
        if (eLeft && s) hasConflict = true;
        // West left conflicts with North straight
        if (vLeft && n) hasConflict = true;

        // 3. Left turn conflicts with opposing left turns
        // North left vs South left (crossing paths)
        if (nLeft && sLeft) hasConflict = true;
        // East left vs West left (crossing paths)
        if (eLeft && vLeft) hasConflict = true;

        // 4. Multiple left turns that cross the intersection center
        // North left conflicts with East left (both cross center)
        if (nLeft && eLeft) hasConflict = true;
        // South left conflicts with West left (both cross center)
        if (sLeft && vLeft) hasConflict = true;
        // North left conflicts with West left
        if (nLeft && vLeft) hasConflict = true;
        // South left conflicts with East left
        if (sLeft && eLeft) hasConflict = true;

        // 5. Optional: Prevent opposite straight lanes (uncomment if needed)
        // if (n && s) hasConflict = true;
        // if (e && v) hasConflict = true;

        return hasConflict;
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var discreteActions = actionsOut.DiscreteActions;
        discreteActions[0] = lightN.isGreen ? 1 : 0;
        discreteActions[1] = lightS.isGreen ? 1 : 0;
        discreteActions[2] = lightE.isGreen ? 1 : 0;
        discreteActions[3] = lightV.isGreen ? 1 : 0;
        discreteActions[4] = lightNLeft.isGreen ? 1 : 0;
        discreteActions[5] = lightSLeft.isGreen ? 1 : 0;
        discreteActions[6] = lightELeft.isGreen ? 1 : 0;
        discreteActions[7] = lightVLeft.isGreen ? 1 : 0;
    }

    public void RewardCarPassed(float waitTime, float speed)
    {
        // 🔧 IMPROVED: Balanced car passing reward
        float waitReward = 1f / (1f + waitTime * 0.1f);  // Less sensitive to wait time
        float speedReward = Mathf.Clamp01(speed / 25f);   // Normalized speed reward
        float baseReward = 1f;
        
        float totalReward = (speedReward + baseReward) * carPassedRewardWeight;
        //Debug.Log($"car passed reward {totalReward}");
        AddReward(totalReward);
    }

    // 🔧 NEW: Method to handle anger-based rewards from the logger
    public void ProcessAngerFeedback(float angerScore, string laneID)
    {
        // This method can be called by the TrafficStatsLogger
        // to provide immediate feedback when anger levels change
        
        if (angerScore > 8f) // High anger threshold
        {
           // AddReward(-0.1f * angerPenaltyWeight); // Immediate penalty for high anger
        }
        else if (angerScore < 2f) // Low anger threshold
        {
            //AddReward(0.05f * angerReductionRewardWeight); // Small reward for keeping anger low
        }
    }

    void Update()
    {
        timer += Time.deltaTime;
        episodeTimer += Time.deltaTime;
        
        if (timer >= decisionInterval)
        {
            timer = 0f;
            RequestDecision();
        }
        
        if (episodeTimer >= maxEpisodeTime)
        {
            // 🔧 FIXED: Positive completion reward
            AddReward(0.1f);
            OnEpisodeEnd();
        }
        
        // Check inactivity only if we have a logger
        if (statsLogger != null && statsLogger.TimeSinceLastReport > maxInactivityTime)
        {
            //Debug.Log($"[EPISODE] Ending due to inactivity (no cars reported) in scene {gameObject.scene.name}");
            AddReward(inactivityPenaltyWeight);
            OnEpisodeEnd(true);
        }
    }
    
    public void OnEpisodeEnd( bool earlyStop = false)
    {
        if (statsLogger != null)
        {
            var (totalUnfinishedWait, totalUnfinishedSpeed, unfinishedCarCount) = statsLogger.ReportUnfinishedCars();
        }
        EndEpisode();
    }
}