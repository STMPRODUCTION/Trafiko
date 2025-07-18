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
    
    // 🔧 NEW: Track previous state to prevent oscillation
    private bool[] previousLightStates = new bool[4];
    private float stabilityRewardWeight = 0.1f;
    
    // 🔧 NEW: Cache the scene-specific logger
    [SerializeField] private TrafficStatsLogger statsLogger;

    public override void OnEpisodeBegin()
    {
        // Ensure we have the logger reference
        if (statsLogger == null)
        {
            statsLogger = TrafficStatsLogger.GetInstanceForGameObject(gameObject);
        }

        // Reset traffic lights
        lightN.SetGreen(false);
        lightS.SetGreen(false);
        lightE.SetGreen(false);
        lightV.SetGreen(false);
        
        // Reset tracking
        episodeTimer = 0f;
        timer = 0f;
        previousLightStates = new bool[4];
        
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
        }
        else
        {
            // Lane observations
            sensor.AddObservation(statsLogger.GetCarsOnLane("N"));
            sensor.AddObservation(statsLogger.GetCarsOnLane("S"));
            sensor.AddObservation(statsLogger.GetCarsOnLane("E"));
            sensor.AddObservation(statsLogger.GetCarsOnLane("V"));

            // 🔧 FIXED: Normalize observations
            sensor.AddObservation(statsLogger.recentAvgWaitTime / 10f); // Normalize by expected max
            sensor.AddObservation(statsLogger.recentAvgSpeed / 10f);    // Normalize by expected max
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

        // Apply light changes
        bool newN = actions.DiscreteActions[0] == 1;
        bool newS = actions.DiscreteActions[1] == 1;
        bool newE = actions.DiscreteActions[2] == 1;
        bool newV = actions.DiscreteActions[3] == 1;

        lightN.SetGreen(newN);
        lightS.SetGreen(newS);
        lightE.SetGreen(newE);
        lightV.SetGreen(newV);

        // --- 🔧 IMPROVED Reward Shaping ---
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
        }
        
        EndEpisode();
    }
}