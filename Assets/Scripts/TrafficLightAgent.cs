using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using UnityEngine;

public class TrafficLightAgent : Agent
{
    public TrafficLight lightN;
    public TrafficLight lightS;
    public TrafficLight lightE;
    public TrafficLight lightV;

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
    
    
    // 🔧 NEW: Track previous state to prevent oscillation
    private bool[] previousLightStates = new bool[4];

    [SerializeField] private float stabilityRewardWeight = 0.1f;
    
    // 🔧 NEW: Track previous anger levels for improvement rewards
    private float previousOverallAnger = 0f;
    private float[] previousLaneAnger = new float[4];
    
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

        // 🔧 NEW: Randomize initial traffic light states
        if (randomizeInitialLights)
        {
            SetRandomInitialLights();
        }
        else
        {
            // Original behavior: all lights off
            lightN.SetGreen(false);
            lightS.SetGreen(false);
            lightE.SetGreen(false);
            lightV.SetGreen(false);
        }
        
        // Reset tracking
        episodeTimer = 0f;
        timer = 0f;
        previousLightStates = new bool[4];
        previousOverallAnger = 0f;
        previousLaneAnger = new float[4];
        
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

        // Debug log the initial state
        Debug.Log($"[EPISODE START] Scene: {gameObject.scene.name}, Lights - N:{lightN.isGreen}, S:{lightS.isGreen}, E:{lightE.isGreen}, V:{lightV.isGreen}");
    }

    // 🔧 NEW: Method to set random initial traffic light states
    private void SetRandomInitialLights()
    {
        if (preventInitialConflicts)
        {
            // Strategy 1: Choose one main direction randomly
            float directionChoice = Random.Range(0f, 1f);
            
            if (directionChoice < 0.33f)
            {
                // North-South direction
                lightN.SetGreen(Random.Range(0f, 1f) < randomGreenProbability);
                lightS.SetGreen(Random.Range(0f, 1f) < randomGreenProbability);
                lightE.SetGreen(false);
                lightV.SetGreen(false);
            }
            else if (directionChoice < 0.66f)
            {
                // East-West direction
                lightN.SetGreen(false);
                lightS.SetGreen(false);
                lightE.SetGreen(Random.Range(0f, 1f) < randomGreenProbability);
                lightV.SetGreen(Random.Range(0f, 1f) < randomGreenProbability);
            }
            else
            {
                // All lights off (traffic jam scenario)
                lightN.SetGreen(false);
                lightS.SetGreen(false);
                lightE.SetGreen(false);
                lightV.SetGreen(false);
            }
        }
        else
        {
            // Strategy 2: Completely random (may have conflicts)
            lightN.SetGreen(Random.Range(0f, 1f) < randomGreenProbability);
            lightS.SetGreen(Random.Range(0f, 1f) < randomGreenProbability);
            lightE.SetGreen(Random.Range(0f, 1f) < randomGreenProbability);
            lightV.SetGreen(Random.Range(0f, 1f) < randomGreenProbability);
        }
    }

    // 🔧 NEW: Alternative method for more sophisticated random initialization
    private void SetSmartRandomInitialLights()
    {
        // Predefined safe configurations
        var safeConfigurations = new bool[][]
        {
            new bool[] { false, false, false, false }, // All red
            new bool[] { true, false, false, false },  // Only North
            new bool[] { false, true, false, false },  // Only South
            new bool[] { false, false, true, false },  // Only East
            new bool[] { false, false, false, true },  // Only West
            new bool[] { true, true, false, false },   // North-South
            new bool[] { false, false, true, true },   // East-West
            new bool[] { true, false, true, false },   // North-East (if allowed)
            new bool[] { false, true, false, true },   // South-West (if allowed)
        };

        // Choose a random configuration
        int configIndex = Random.Range(0, safeConfigurations.Length);
        bool[] chosenConfig = safeConfigurations[configIndex];

        lightN.SetGreen(chosenConfig[0]);
        lightS.SetGreen(chosenConfig[1]);
        lightE.SetGreen(chosenConfig[2]);
        lightV.SetGreen(chosenConfig[3]);
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (statsLogger == null)
        {
            // Fallback: Add zero observations if logger is missing
            sensor.AddObservation(0f); // N lane cars
            sensor.AddObservation(0f); // S lane cars
            sensor.AddObservation(0f); // E lane cars
            sensor.AddObservation(0f); // V lane cars
            sensor.AddObservation(0f); // recent avg wait time
            sensor.AddObservation(0f); // recent avg speed
            sensor.AddObservation(0f); // cars on intersection
            
            // 🔧 NEW: Add zero anger observations
            sensor.AddObservation(0f); // overall anger
            sensor.AddObservation(0f); // peak anger
            sensor.AddObservation(0f); // recent avg anger
            sensor.AddObservation(0f); // N lane anger
            sensor.AddObservation(0f); // S lane anger
            sensor.AddObservation(0f); // E lane anger
            sensor.AddObservation(0f); // V lane anger
        }
        else
        {
            // Lane observations
            sensor.AddObservation(statsLogger.GetCarsOnLane("N"));
            sensor.AddObservation(statsLogger.GetCarsOnLane("S"));
            sensor.AddObservation(statsLogger.GetCarsOnLane("E"));
            sensor.AddObservation(statsLogger.GetCarsOnLane("V"));
            sensor.AddObservation(statsLogger.GetCarsInIntersection());

            // 🔧 FIXED: Normalize observations
            sensor.AddObservation(statsLogger.recentAvgWaitTime / 10f); // Normalize by expected max
            sensor.AddObservation(statsLogger.recentAvgSpeed / 10f);    // Normalize by expected max
            
            // 🔧 NEW: Add anger observations (normalized)
            var (avgAnger, peakAnger, recentAvgAnger, totalReports) = statsLogger.GetOverallAngerStats();
            sensor.AddObservation(Mathf.Clamp01(recentAvgAnger / 10f));  // Recent average anger (normalized)
            
            // 🔧 NEW: Add per-lane anger observations
            string[] lanes = { "N", "S", "E", "V" };
            for (int i = 0; i < lanes.Length; i++)
            {
                var laneAngerStats = statsLogger.GetLaneAngerStats(lanes[i]);
                float laneAnger = laneAngerStats != null ? laneAngerStats.avgAnger : 0f;
                sensor.AddObservation(Mathf.Clamp01(laneAnger / 10f)); // Normalize lane anger
            }
        }

        // Current light states
        sensor.AddObservation(lightN.isGreen ? 1 : 0);
        sensor.AddObservation(lightS.isGreen ? 1 : 0);
        sensor.AddObservation(lightE.isGreen ? 1 : 0);
        sensor.AddObservation(lightV.isGreen ? 1 : 0);
        
        // 🔧 NEW: Add time-based observations
        sensor.AddObservation(timer / decisionInterval); // Decision cycle progress
        sensor.AddObservation(episodeTimer / maxEpisodeTime); // Episode progress
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        // Store previous states
        previousLightStates[0] = lightN.isGreen;
        previousLightStates[1] = lightS.isGreen;
        previousLightStates[2] = lightE.isGreen;
        previousLightStates[3] = lightV.isGreen;

        // 🔧 NEW: Store previous anger levels
        if (statsLogger != null)
        {
            var (avgAnger, _, _, _) = statsLogger.GetOverallAngerStats();
            previousOverallAnger = avgAnger;
            
            string[] lanes = { "N", "S", "E", "V" };
            for (int i = 0; i < lanes.Length; i++)
            {
                var laneStats = statsLogger.GetLaneAngerStats(lanes[i]);
                previousLaneAnger[i] = laneStats != null ? laneStats.avgAnger : 0f;
            }
        }

        // Apply light changes
        bool newN = actions.DiscreteActions[0] == 1;
        bool newS = actions.DiscreteActions[1] == 1;
        bool newE = actions.DiscreteActions[2] == 1;
        bool newV = actions.DiscreteActions[3] == 1;

        lightN.SetGreen(newN);
        lightS.SetGreen(newS);
        lightE.SetGreen(newE);
        lightV.SetGreen(newV);

        // --- 🔧 IMPROVED Reward Shaping with Anger Integration ---
        float reward = 0f;

        if (statsLogger != null)
        {
            // ✅ Reward for throughput (normalized)
            float normalizedSpeed = Mathf.Clamp01(statsLogger.recentAvgSpeed / 10f);
            reward += normalizedSpeed * speedRewardWeight;

            // ❌ Penalize waiting (normalized)
            float normalizedWait = Mathf.Clamp01(statsLogger.recentAvgWaitTime / 20f);
            reward -= normalizedWait * waitPenaltyWeight;

            // ❌ Congestion penalty (scaled)
            foreach (var lane in new[] { "N", "S", "E", "V" })
            {
                int count = statsLogger.GetCarsOnLane(lane);
                if (count > 5)
                {
                    reward -= congestionPenaltyWeight * (count - 5);
                }
            }
            
            // 🔧 NEW: Anger-based rewards and penalties
            var (avgAnger, peakAnger, recentAvgAnger, totalReports) = statsLogger.GetOverallAngerStats();
            
            // ❌ Penalize high anger levels
            float normalizedAnger = Mathf.Clamp01(recentAvgAnger / 10f);
            reward -= normalizedAnger * angerPenaltyWeight;
            
            // ✅ Reward for reducing anger (improvement over time)
            if (totalReports > 0 && avgAnger < previousOverallAnger)
            {
                float angerReduction = previousOverallAnger - avgAnger;
                reward += angerReduction * angerReductionRewardWeight;
            }
            
            // 🔧 NEW: Lane-specific anger management rewards
            string[] lanes = { "N", "S", "E", "V" };
            bool[] currentLightStates = { newN, newS, newE, newV };
            
            for (int i = 0; i < lanes.Length; i++)
            {
                var laneStats = statsLogger.GetLaneAngerStats(lanes[i]);
                if (laneStats != null && laneStats.angerReports > 0)
                {
                    float currentLaneAnger = laneStats.avgAnger;
                    
                    // ✅ Reward for prioritizing angry lanes
                    if (currentLaneAnger > 5f && currentLightStates[i])
                    {
                        reward += 0.2f; // Bonus for giving green light to angry lane
                    }
                    
                    // ❌ Penalty for ignoring very angry lanes
                    if (currentLaneAnger > 8f && !currentLightStates[i])
                    {
                        reward -= 0.3f; // Penalty for not addressing high anger
                    }
                }
            }
        }

        // ❌ 🔧 FIXED: Proper conflict detection for intersection
        bool hasConflict = false;
        
        // North-South vs East-West conflicts
        if ((newN || newS) && (newE || newV))
        {
            hasConflict = true;
        }
        
        // Opposite direction conflicts (if your intersection doesn't allow this)
        // Uncomment if N-S shouldn't be green simultaneously:
        // if (newN && newS) hasConflict = true;
        // if (newE && newV) hasConflict = true;

        if (hasConflict)
        {
            reward -= conflictPenaltyWeight;
        }

        // ✅ 🔧 NEW: Stability reward (prevent oscillation)
        bool[] currentStates = { newN, newS, newE, newV };
        int changedLights = 0;
        for (int i = 0; i < 4; i++)
        {
            if (currentStates[i] != previousLightStates[i])
                changedLights++;
        }
        
        // Small penalty for changing too many lights at once
        if (changedLights > 2)
        {
            reward -= stabilityRewardWeight;
        }

        // ✅ 🔧 NEW: Reward for having at least one direction green
        if (newN || newS || newE || newV)
        {
            reward += 0.1f; // Small reward for allowing traffic flow
        }

        if (newN && newS && !newE && !newV)
        {
           // reward += 0.2f; // Small reward for allowing N-S and E-V pairs
        }

        AddReward(reward);
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var discreteActions = actionsOut.DiscreteActions;
        discreteActions[0] = lightN.isGreen ? 1 : 0;
        discreteActions[1] = lightS.isGreen ? 1 : 0;
        discreteActions[2] = lightE.isGreen ? 1 : 0;
        discreteActions[3] = lightV.isGreen ? 1 : 0;
    }

    public void RewardCarPassed(float waitTime, float speed)
    {
        // 🔧 IMPROVED: Balanced car passing reward
        float waitReward = 1f / (1f + waitTime * 0.1f);  // Less sensitive to wait time
        float speedReward = Mathf.Clamp01(speed / 10f);   // Normalized speed reward
        float baseReward = 1f;
        
        float totalReward = (waitReward + speedReward + baseReward) * carPassedRewardWeight;
        AddReward(totalReward);
    }

    // 🔧 NEW: Method to handle anger-based rewards from the logger
    public void ProcessAngerFeedback(float angerScore, string laneID)
    {
        // This method can be called by the TrafficStatsLogger
        // to provide immediate feedback when anger levels change
        
        if (angerScore > 8f) // High anger threshold
        {
            AddReward(-0.1f); // Immediate penalty for high anger
        }
        else if (angerScore < 2f) // Low anger threshold
        {
            AddReward(0.05f); // Small reward for keeping anger low
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
            AddReward(2f);
            OnEpisodeEnd();
        }
        
        // Check inactivity only if we have a logger
        if (statsLogger != null && statsLogger.TimeSinceLastReport > maxInactivityTime)
        {
            Debug.Log($"[EPISODE] Ending due to inactivity (no cars reported) in scene {gameObject.scene.name}");
            AddReward(-1f);
            OnEpisodeEnd();
        }
    }
    
    public void OnEpisodeEnd()
    {
        if (statsLogger != null)
        {
            var (totalUnfinishedWait, totalUnfinishedSpeed, unfinishedCarCount) = statsLogger.ReportUnfinishedCars();

            if (unfinishedCarCount > 0)
            {
                float avgUnfinishedWait = totalUnfinishedWait / unfinishedCarCount;
                float avgUnfinishedSpeed = totalUnfinishedSpeed / unfinishedCarCount;

                // 🔧 IMPROVED: Scaled penalties
                float waitPenalty = -(avgUnfinishedWait / 20f) * unfinishedCarPenaltyWeight;
                float speedPenalty = -(1f - Mathf.Clamp01(avgUnfinishedSpeed / 10f)) * unfinishedCarPenaltyWeight;
                float carCountPenalty = -(unfinishedCarCount / 10f) * unfinishedCarPenaltyWeight;
                
                float totalPenalty = waitPenalty + speedPenalty + carCountPenalty;
                AddReward(totalPenalty);
                
                Debug.Log($"End episode in scene {gameObject.scene.name} - Speed penalty: {speedPenalty:F3}, Wait penalty: {waitPenalty:F3}, Car count penalty: {carCountPenalty:F3}");
            }
            
            // 🔧 NEW: Final anger-based rewards/penalties
            var (avgAnger, peakAnger, recentAvgAnger, totalReports) = statsLogger.GetOverallAngerStats();
            
            if (totalReports > 0)
            {
                // Final penalty for high average anger
                float finalAngerPenalty = -(avgAnger / 10f) * angerPenaltyWeight;
                AddReward(finalAngerPenalty);
                
                // Bonus for keeping anger low throughout episode
                if (avgAnger < 3f)
                {
                    AddReward(0.5f); // Bonus for low anger episode
                }
                
                Debug.Log($"End episode anger stats - Avg: {avgAnger:F2}, Peak: {peakAnger:F2}, Final penalty: {finalAngerPenalty:F3}");
            }
            if(statsLogger.AcidentInstances==0)
            {
                AddReward(1f*NoAccidentReward);
            }
        }
        
        EndEpisode();
    }
}