using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

public class SimpleTrafficAgent : Agent
{
    [Header("Traffic Light References")]
    [SerializeField] private TrafficLight lightN;
    [SerializeField] private TrafficLight lightS;
    [SerializeField] private TrafficLight lightE;
    [SerializeField] private TrafficLight lightV;
    [SerializeField] private TrafficLight lightNLeft;
    [SerializeField] private TrafficLight lightSLeft;
    [SerializeField] private TrafficLight lightELeft;
    [SerializeField] private TrafficLight lightVLeft;

    [Header("Intersection Counter")]
    [SerializeField] private IntersectionCounter intersectionCounter;

    [Header("Agent Parameters")]
    [SerializeField] private float decisionInterval = 3f;
    [SerializeField] private float maxEpisodeTime = 300f;
    [SerializeField] private float minLightDuration = 2f; // Minimum time a light must stay in one state

    [Header("Reward Weights")]
    [SerializeField] private float waitPenalty = 0.5f;
    [SerializeField] private float congestionPenalty = 0.3f;
        [SerializeField] private float CarLeftRewardWeight = 0.1f;


    [Header("Time Scale Control")]
    [Range(1, 20)]
    public int timeScale = 1;

    // Internal state tracking
    private float timer = 0f;
    private float episodeTimer = 0f;
    private int currentConfigurationIndex = 0;
    private float[] lightStateDurations = new float[8]; // Track how long each light has been in current state
    private bool[] previousLightStates = new bool[8];
    
    // Predefined safe traffic light configurations
    private bool[][] trafficConfigurations;

    private void Awake()
    {
        intersectionCounter.OnCarLeft += CarLeftReward;
        InitializeTrafficConfigurations();
    }
    private int CarsLeft = 0;
    public void CarLeftReward()
    {
        CarsLeft++;
    }

    private void InitializeTrafficConfigurations()
    {
        // Safe configurations for 8 lanes (N, S, E, V straight + left turns)
        // Format: [N, S, E, V, NLeft, SLeft, ELeft, VLeft]
        trafficConfigurations = new bool[][]
        {
            // All red (safety/transition state)
            new bool[] { false, false, false, false, false, false, false, false },
            
            // North-South straight only
            new bool[] { true, true, false, false, false, false, false, false },
            
            // East-West straight only  
            new bool[] { false, false, true, true, false, false, false, false },
            
            // North straight + North left (protected left turn)
            new bool[] { true, false, false, false, true, false, false, false },
            
            // South straight + South left (protected left turn)
            new bool[] { false, true, false, false, false, true, false, false },
            
            // East straight + East left (protected left turn)
            new bool[] { false, false, true, false, false, false, true, false },
            
            // West straight + West left (protected left turn)
            new bool[] { false, false, false, true, false, false, false, true },
            
            // North-South left turns only
            new bool[] { false, false, false, false, true, true, false, false },
            
            // East-West left turns only
            new bool[] { false, false, false, false, false, false, true, true }
        };
    }

    public override void OnEpisodeBegin()
    {
        GameObject[] cars = GameObject.FindGameObjectsWithTag("Car");
        foreach (GameObject car in cars)
        {
            if (car != null)
            {
                Destroy(car);
            }
        }

        timer = 0f;
        episodeTimer = 0f;
        currentConfigurationIndex = 0;
        
        for (int i = 0; i < lightStateDurations.Length; i++)
        {
            lightStateDurations[i] = 0f;
        }
        
        // Start with all red configuration
        SetTrafficConfiguration(0);
        
        Debug.Log("Traffic Agent Episode Begin");
    }

    private void Update()
    {
        // Apply time scale control
        Time.timeScale = timeScale;
        
        timer += Time.deltaTime;
        episodeTimer += Time.deltaTime;
        
        // Update light state durations
        UpdateLightStateDurations();
        
        // Request decision at intervals
        if (timer >= decisionInterval)
        {
            //Debug.Log("decided");
            RequestDecision();
            timer = 0f;
        }
        
        // End episode if max time reached
        if (episodeTimer >= maxEpisodeTime)
        {
            EndEpisode();
        }
    }

    private void UpdateLightStateDurations()
    {
        bool[] currentStates = {
            lightN.isGreen, lightS.isGreen, lightE.isGreen, lightV.isGreen,
            lightNLeft.isGreen, lightSLeft.isGreen, lightELeft.isGreen, lightVLeft.isGreen
        };
        
        for (int i = 0; i < currentStates.Length; i++)
        {
            if (currentStates[i] == previousLightStates[i])
            {
                lightStateDurations[i] += Time.deltaTime;
            }
            else
            {
                lightStateDurations[i] = 0f;
                previousLightStates[i] = currentStates[i];
            }
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(intersectionCounter.GetCarsAtEntrance(0)); // N extreme
        sensor.AddObservation(intersectionCounter.GetCarsAtEntrance(1)); // N centre
        sensor.AddObservation(intersectionCounter.GetCarsAtEntrance(2)); // S extreme
        sensor.AddObservation(intersectionCounter.GetCarsAtEntrance(3)); // S centre
        sensor.AddObservation(intersectionCounter.GetCarsAtEntrance(4)); // E extreme
        sensor.AddObservation(intersectionCounter.GetCarsAtEntrance(5)); // E centre
        sensor.AddObservation(intersectionCounter.GetCarsAtEntrance(6)); // V extreme
        sensor.AddObservation(intersectionCounter.GetCarsAtEntrance(7)); // V centre

        sensor.AddObservation(intersectionCounter.GetWaitTimeAtEntrance(0)); // N extreme
        sensor.AddObservation(intersectionCounter.GetWaitTimeAtEntrance(1)); // N centre
        sensor.AddObservation(intersectionCounter.GetWaitTimeAtEntrance(2)); // S extreme
        sensor.AddObservation(intersectionCounter.GetWaitTimeAtEntrance(3)); // S centre
        sensor.AddObservation(intersectionCounter.GetWaitTimeAtEntrance(4)); // E extreme
        sensor.AddObservation(intersectionCounter.GetWaitTimeAtEntrance(5)); // E centre
        sensor.AddObservation(intersectionCounter.GetWaitTimeAtEntrance(6)); // V extreme
        sensor.AddObservation(intersectionCounter.GetWaitTimeAtEntrance(7)); // V centre

        sensor.AddObservation(intersectionCounter.GetCarsWaitingInIntersection());
        sensor.AddObservation(intersectionCounter.GetCarsInIntersection());
        // Current light states (8 lights)
        sensor.AddObservation(lightN.isGreen ? 1f : 0f);
        sensor.AddObservation(lightS.isGreen ? 1f : 0f);
        sensor.AddObservation(lightE.isGreen ? 1f : 0f);
        sensor.AddObservation(lightV.isGreen ? 1f : 0f);
        sensor.AddObservation(lightNLeft.isGreen ? 1f : 0f);
        sensor.AddObservation(lightSLeft.isGreen ? 1f : 0f);
        sensor.AddObservation(lightELeft.isGreen ? 1f : 0f);
        sensor.AddObservation(lightVLeft.isGreen ? 1f : 0f);

        // Light state durations (normalized by decision interval)
        for (int i = 0; i < lightStateDurations.Length; i++)
        {
            sensor.AddObservation(lightStateDurations[i] / decisionInterval);
        }
        sensor.AddObservation(currentConfigurationIndex);
        sensor.AddObservation(timer / decisionInterval);

    }
    public override void OnActionReceived(ActionBuffers actions)
    {
        // Agent chooses from predefined safe configurations
        int chosenConfigIndex = actions.DiscreteActions[0];
        
        // Validate and clamp the action
        chosenConfigIndex = Mathf.Clamp(chosenConfigIndex, 0, trafficConfigurations.Length - 1);
        
        // Check if we can change lights (minimum duration constraint)
        if (CanChangeLights() || chosenConfigIndex == 0) // Always allow all-red for safety
        {
            SetTrafficConfiguration(chosenConfigIndex);
        }
        
        // Calculate and apply rewards
        float reward = CalculateReward();
        AddReward(reward);
        
        Debug.Log($"Action: Config {chosenConfigIndex}, Reward: {reward:F3}, Cumulative: {GetCumulativeReward():F3}");
    }

    private bool CanChangeLights()
    {
        // Check if minimum duration has passed for any currently green lights
        for (int i = 0; i < lightStateDurations.Length; i++)
        {
            if (previousLightStates[i] && lightStateDurations[i] < minLightDuration)
            {
                return false;
            }
        }
        return true;
    }
    public float inIntersectionReward = 0f;
    private float CalculateReward()
    {
        float reward = 0f;
        int carsAtEntrance = intersectionCounter.GetCarsWaitingInIntersection();       
        reward -= carsAtEntrance * congestionPenalty;

        float totalWait = intersectionCounter.GetTotalWaitingTime();
        reward -= totalWait * waitPenalty;

        if (currentConfigurationIndex == 0)
        {
            reward -= 0.01f;
        }
        reward += CarsLeft * CarLeftRewardWeight;
        reward += intersectionCounter.GetCarsInIntersection() * inIntersectionReward;
        if (currentConfigurationIndex == 0)
        {
            reward -= 0.01f;
        }
        Debug.Log(CarsLeft);
        CarsLeft = 0;
        return reward;
        
    }
    private void SetTrafficConfiguration(int configIndex)
    {
        if (configIndex < 0 || configIndex >= trafficConfigurations.Length)
        {
            Debug.LogWarning($"Invalid configuration index: {configIndex}. Using all-red.");
            configIndex = 0;
        }
        
        currentConfigurationIndex = configIndex;
        bool[] config = trafficConfigurations[configIndex];
        
        // Apply the configuration to all lights
        if (lightN != null) lightN.SetGreen(config[0]);
        if (lightS != null) lightS.SetGreen(config[1]);
        if (lightE != null) lightE.SetGreen(config[2]);
        if (lightV != null) lightV.SetGreen(config[3]);
        if (lightNLeft != null) lightNLeft.SetGreen(config[4]);
        if (lightSLeft != null) lightSLeft.SetGreen(config[5]);
        if (lightELeft != null) lightELeft.SetGreen(config[6]);
        if (lightVLeft != null) lightVLeft.SetGreen(config[7]);
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        // Simple heuristic: cycle through configurations
        int heuristicAction = ((int)(episodeTimer / decisionInterval) % trafficConfigurations.Length);
        var discreteActions = actionsOut.DiscreteActions;
        discreteActions[0] = heuristicAction;
    }
}


