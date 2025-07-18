using UnityEngine;

public class CarController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float acceleration = 5f;
    public float maxSpeed = 10f;
    public float stopDistanceToLight = 10f;
    public float stopDistanceToCar = 5f;
    public float decelerationDistance = 10f;

    [Header("Anger System")]
    public float angerGrowthRate = 0.5f; // Base exponential growth rate
    public float angerLogInterval = 2f; // Log anger every 2 seconds
    public float waitTimeThreshold = 1f; // Minimum wait time before anger starts growing
    
    public TrafficLight targetLight;
    public float currentSpeed = 0f;
    public float ray_Y_Origin = 0f;
    public float deceleration = 1f;
    public string laneID;
    public float avgSpeed;

    [SerializeField] private float distanceToTrafficLight = 30f;
    public float spawnTime;
    public float timeTaken;

    public TrafficStatsLogger statsLogger;

    [Header("Performance Settings")]
    public LayerMask carLayerMask = -1;
    public float lightCheckInterval = 0.1f; // Check light every 0.1 seconds instead of every frame
    public float carDetectionInterval = 0.05f; // Check for cars every 0.05 seconds

    // Performance optimization variables
    private float lightCheckTimer;
    private float carDetectionTimer;
    private bool isNearLight = false;
    private bool accidentReported = false;
    private bool hasPassedLight = false;
    private bool isDestroyed = false;
    private bool hasEnteredIntersection = false; // New flag for intersection tracking

    // Anger system variables
    private float angerScore = 0f;
    private float waitStartTime = 0f;
    private bool isWaiting = false;
    private float angerLogTimer = 0f;
    private float totalWaitTime = 0f;

    // Cached calculations
    private float lastLightDistance = float.MaxValue;
    private bool lastLightState = true;
    private float cachedCarDistance = float.MaxValue;
    private float cachedCarBrakeFactor = 0f;
    private bool cachedLightHardStop = false;
    private float cachedLightBrakeFactor = 0f;

    // Pre-calculated values
    private float decelerationRange;
    private float slowestAllowedSpeed;
    private float accelerationTimesDelta;
    private float decelerationTimesDelta;

    void Start()
    {
        spawnTime = Time.time;
        if (targetLight != null)
        {
            distanceToTrafficLight = Vector3.Distance(transform.position, targetLight.transform.position);
        }

        // Pre-calculate commonly used values
        decelerationRange = decelerationDistance - stopDistanceToLight;
        slowestAllowedSpeed = maxSpeed * 0.1f;
        
        // Initialize timers with random offset to spread load
        lightCheckTimer = Random.Range(0f, lightCheckInterval);
        carDetectionTimer = Random.Range(0f, carDetectionInterval);
        angerLogTimer = Random.Range(0f, angerLogInterval); // Spread anger logging too
    }

    void Update()
    {
        //if (isDestroyed) return;

        // Cache deltaTime to avoid multiple calls
        float deltaTime = Time.deltaTime;
        accelerationTimesDelta = acceleration * deltaTime;
        decelerationTimesDelta = acceleration * deceleration * deltaTime;

        float brakeFactor = 0f;
        bool hardStop = false;

        // 🔴 Check RED light (less frequently)
        lightCheckTimer -= deltaTime;
        if (lightCheckTimer <= 0f)
        {
            lightCheckTimer = lightCheckInterval;
            CheckTrafficLight(out cachedLightBrakeFactor, out cachedLightHardStop);
        }

        // Use cached light values
        brakeFactor = cachedLightBrakeFactor;
        hardStop = cachedLightHardStop;

        // 🚗 Check car in front (less frequently)
        carDetectionTimer -= deltaTime;
        if (carDetectionTimer <= 0f)
        {
            carDetectionTimer = carDetectionInterval;
            UpdateCarDetection();
        }

        // Use cached car detection results
        if (cachedCarDistance <= stopDistanceToCar)
        {
            hardStop = true;
        }
        else
        {
            brakeFactor = Mathf.Max(brakeFactor, cachedCarBrakeFactor);
        }

        // 😠 Update anger system
        UpdateAngerSystem(hardStop || currentSpeed < slowestAllowedSpeed, deltaTime);

        // 🚦 Final movement
        UpdateMovement(brakeFactor, hardStop, deltaTime);
    }

    void UpdateAngerSystem(bool isCurrentlyWaiting, float deltaTime)
    {
        // Start waiting if we're stopped/slow and weren't waiting before
        if (isCurrentlyWaiting && !isWaiting)
        {
            isWaiting = true;
            waitStartTime = Time.time;
        }
        // Stop waiting if we're moving and were waiting before
        else if (!isCurrentlyWaiting && isWaiting)
        {
            isWaiting = false;
            totalWaitTime += Time.time - waitStartTime;
        }

        // Update anger score if we're waiting
        if (isWaiting)
        {
            float currentWaitTime = Time.time - waitStartTime;
            if (currentWaitTime > waitTimeThreshold)
            {
                // Exponential growth: anger = base * e^(rate * waitTime)
                float waitTimeForAnger = currentWaitTime - waitTimeThreshold;
                angerScore = Mathf.Exp(angerGrowthRate * waitTimeForAnger) - 1f;
            }
        }

        // Log anger data periodically
        angerLogTimer -= deltaTime;
        if (angerLogTimer <= 0f)
        {
            angerLogTimer = angerLogInterval;
            LogAngerData();
        }
    }

    void LogAngerData()
    {
        if (statsLogger != null)
        {
            float currentWaitTime = isWaiting ? (Time.time - waitStartTime) : 0f;
            statsLogger.ReportAngerData(laneID, angerScore, totalWaitTime + currentWaitTime);
        }
    }

    void CheckTrafficLight(out float lightBrakeFactor, out bool lightHardStop)
    {
        lightBrakeFactor = 0f;
        lightHardStop = false;

        if (targetLight == null) return;

        Vector3 toLight = targetLight.transform.position - transform.position;
        float forwardDot = Vector3.Dot(toLight.normalized, transform.forward);
        float distanceToLight = toLight.magnitude;

        // Cache values for next frames
        lastLightDistance = distanceToLight;
        lastLightState = targetLight.isGreen;
        isNearLight = distanceToLight <= decelerationDistance;

        // Report entering intersection when moving forward toward light and close enough
        if (forwardDot < 0.0f && !hasEnteredIntersection && distanceToLight <= stopDistanceToLight)
        {
            hasEnteredIntersection = true;
            statsLogger?.ReportCarEnterIntersection(laneID);
        }

        if (forwardDot > 0.1f && !targetLight.isGreen)
        {
            if (distanceToLight <= stopDistanceToLight)
            {
                lightHardStop = true;
            }
            else if (distanceToLight <= decelerationDistance)
            {
                lightBrakeFactor = (decelerationDistance - distanceToLight) / decelerationRange;
            }
        }
    }

    void UpdateCarDetection()
    {
        RaycastHit hit;
        Vector3 rayOrigin = transform.position + Vector3.up * ray_Y_Origin;
        
        // Only draw debug ray in editor for performance
        #if UNITY_EDITOR
        Debug.DrawRay(rayOrigin, transform.forward * decelerationDistance, Color.red);
        #endif

        if (Physics.Raycast(rayOrigin, transform.forward, out hit, decelerationDistance, carLayerMask))
        {
            if (hit.collider.CompareTag("Car"))
            {
                cachedCarDistance = hit.distance;
                
                if (cachedCarDistance <= decelerationDistance && cachedCarDistance > stopDistanceToCar)
                {
                    cachedCarBrakeFactor = (decelerationDistance - cachedCarDistance) / (decelerationDistance - stopDistanceToCar);
                }
                else
                {
                    cachedCarBrakeFactor = 0f;
                }
                return;
            }
        }

        cachedCarDistance = float.MaxValue;
        cachedCarBrakeFactor = 0f;
    }

    void UpdateMovement(float brakeFactor, bool hardStop, float deltaTime)
    {
        if (hardStop || accidentReported)
        {
            currentSpeed = 0f;
            return;
        }

        float desiredSpeed = maxSpeed;

        if (brakeFactor > 0f)
        {
            desiredSpeed = Mathf.Lerp(maxSpeed, slowestAllowedSpeed, brakeFactor);
        }

        // Smoothly adjust toward desiredSpeed
        if (currentSpeed > desiredSpeed)
        {
            currentSpeed = Mathf.Max(currentSpeed - decelerationTimesDelta, desiredSpeed);
        }
        else
        {
            currentSpeed = Mathf.Min(currentSpeed + accelerationTimesDelta, maxSpeed);
        }

        // Move the car
        transform.Translate(Vector3.forward * currentSpeed * deltaTime);
    }

    void OnTriggerEnter(Collider other)
    {
        if (isDestroyed) return;

        if (!hasPassedLight && other.CompareTag($"IntersectionTrigger{laneID}"))
        {
            hasPassedLight = true;

            float timeTaken = Time.time - spawnTime;
            avgSpeed = distanceToTrafficLight / timeTaken;

            // Final anger data log before destruction
            LogAngerData();

            statsLogger?.ReportCar(timeTaken, laneID, avgSpeed);
            statsLogger?.ReportCarDestroyed(laneID);
            if (hasEnteredIntersection)
            {
                statsLogger?.ReportCarExitIntersection(laneID);
            }

            DestroyCar(5f);
        }
        else if (other.CompareTag("Car") && !accidentReported)
        {
            CarController otherCar = other.GetComponent<CarController>();
            if (otherCar != null && otherCar.laneID != this.laneID)
            {
                accidentReported = true;
                currentSpeed = 0f;
                
                // Log final anger data for accident
                LogAngerData();
                
                statsLogger?.ReportAccident(this.laneID);
                DestroyCar(5f);
            }
        }
    }

    void DestroyCar(float delay = 0f)
    {
        if (isDestroyed) return;
        
        isDestroyed = true;
        
        if (delay > 0f)
        {
            Invoke(nameof(DestroyCarImmediate), delay);
        }
        else
        {
            DestroyCarImmediate();
        }
    }

    void DestroyCarImmediate()
    {
        if (gameObject != null)
        {
            
            Destroy(gameObject);
        }
    }

    // Public getter for anger score (useful for debugging or UI)
    public float GetAngerScore()
    {
        return angerScore;
    }

    // Public getter for total wait time (useful for debugging or UI)
    public float GetTotalWaitTime()
    {
        float currentWaitTime = isWaiting ? (Time.time - waitStartTime) : 0f;
        return totalWaitTime + currentWaitTime;
    }
}