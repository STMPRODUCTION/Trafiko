using UnityEngine;

public class CarController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float acceleration = 5f;
    public float maxSpeed = 10f;
    public float stopDistanceToLight = 10f;
    public float stopDistanceToCar = 5f;
    public float decelerationDistance = 10f;

    [Header("Right Turn Settings")]
    [Range(0f, 1f)]
    public float rightTurnChance = 0.3f; // 30% chance to turn right
    public float baseTurnSpeed = 90f; // Base degrees per second for turning
    public float turnSpeedMultiplier = 2f; // How much speed affects turn rate
    public float minTurnSpeed = 45f; // Minimum turn speed
    public float maxTurnSpeed = 180f; // Maximum turn speed
    public bool isGoingRight = false;

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

    // Right turn variables
    private bool isTurning = false;
    private float targetRotationY;
    private float initialRotationY;
    private bool turnStarted = false;
    private string originalLaneID;

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

    private float totalRotation = 0f;
    private float initialAngle;
    private Vector3 initialPosition;
    private Vector3 previousPosition;
    private float targetTotalRotation = 90f; // for 90-degree turn
    private float turnRadius;

    public Vector3 turnPoint;

    void Start()
    {
        spawnTime = Time.time;
        if (targetLight != null)
        {
            distanceToTrafficLight = Vector3.Distance(transform.position, targetLight.transform.position);
        }

        // Store original lane ID for logging purposes
        originalLaneID = laneID;

        // Randomly determine if this car will turn right
        isGoingRight = Random.Range(0f, 1f) < rightTurnChance;

        // Store initial rotation for turn calculation
        initialRotationY = transform.eulerAngles.y;
        targetRotationY = initialRotationY + 90f; // Target is 90 degrees to the right

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

        // Handle turning after passing the light
        if (isTurning)
        {
            HandleRightTurn(deltaTime);
            return; // Don't do normal movement logic while turning
        }

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

    [SerializeField] private float  normalizeFactor;
    public bool isClockwise;
    void StartRightTurn(bool clockwise = false)
    {
        isTurning = true;
        totalRotation = 0f;
        initialPosition = transform.position;
        previousPosition = transform.position;
        isClockwise = clockwise; // Store the direction
        
        // Calculate initial angle relative to turn point
        Vector3 toInitialPos = initialPosition - turnPoint;
        initialAngle = Mathf.Atan2(toInitialPos.z, toInitialPos.x) * Mathf.Rad2Deg;
        
        // Calculate the radius for this turn
        turnRadius = Vector3.Distance(initialPosition, turnPoint);
    }

    // Call this in your update loop while turning
    void HandleRightTurn(float deltaTime)
    {
        // Instead of angular speed, calculate based on linear speed
        // Linear speed = angular speed × radius
        // So: angular speed = linear speed ÷ radius
        
        float linearSpeed = currentSpeed; // Use the car's actual current speed
        float angularSpeed = linearSpeed / turnRadius; // Convert to angular speed (radians per second)
        float angularSpeedDegrees = angularSpeed * Mathf.Rad2Deg; // Convert to degrees per second
        
        // Apply speed limits if needed
        angularSpeedDegrees = Mathf.Clamp(angularSpeedDegrees, minTurnSpeed, maxTurnSpeed);
        
        float rotationThisFrame = angularSpeedDegrees * deltaTime;

        // Track total rotation
        totalRotation += rotationThisFrame;

        // Calculate new position on circle
        // Use direction multiplier for clockwise/counterclockwise
        float directionMultiplier = isClockwise ? 1f : -1f;
        float currentAngle = initialAngle + (totalRotation * directionMultiplier);
        
        Vector3 newPosition = turnPoint + new Vector3(
            Mathf.Cos(currentAngle * Mathf.Deg2Rad) * turnRadius,
            initialPosition.y, // Keep the same Y as when we started the turn
            Mathf.Sin(currentAngle * Mathf.Deg2Rad) * turnRadius
        );
        
        transform.position = newPosition;

        // Calculate direction of movement for rotation
        Vector3 movementDirection = (newPosition - previousPosition).normalized;
        
        // If we have meaningful movement, update rotation
        if (movementDirection.magnitude > 0.01f)
        {
            transform.rotation = Quaternion.LookRotation(movementDirection, Vector3.up);
        }
        
        // Store current position for next frame
        previousPosition = newPosition;

        // Check if turn is complete (90 degrees for right turn)
        if (totalRotation >= targetTotalRotation)
        {
            isTurning = false;
            
            // Snap to final rotation if needed
            transform.rotation = Quaternion.Euler(transform.eulerAngles.x, targetRotationY, transform.eulerAngles.z);
            
            ChangeLaneAfterTurn();
        }
    }
    void ChangeLaneAfterTurn()
    {
        string newLaneID = GetNewLaneIDAfterRightTurn(originalLaneID);
        
        if (newLaneID != laneID)
        {
            string previousLaneID = laneID;
            laneID = newLaneID;
            
            // Log the lane change
            if (statsLogger != null)
            {
               //Debug.Log($"Car changed lane from {previousLaneID} to {laneID} after right turn");
                
                // If your TrafficStatsLogger has a method for lane changes, uncomment this:
                statsLogger.ReportLaneChange(previousLaneID, laneID);
            }
        }
    }

    string GetNewLaneIDAfterRightTurn(string currentLaneID)
    {
        // Map lane changes after right turns
        // Add more mappings as needed for your specific intersection setup
        switch (currentLaneID.ToUpper())
        {
            case "N":
                return "V";
            case "S":
                return "E";
            case "E":
                return "N";
            case "V":
                return "S";
            default:
                return currentLaneID;
        }
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

            // Start turning if this car is supposed to go right
            if (isGoingRight && !turnStarted)
            {
                StartRightTurn();
                turnStarted = true;
                isTurning = true;
            }
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
        
        if (!isTurning)
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
    // Public getter to check original lane ID (useful for debugging or UI)
    public string GetOriginalLaneID()
    {
        return originalLaneID;
    }
    // Public methods for spawner
    public void SetCornerPoint(Vector3 corner)
    {
        turnPoint = corner;
    }

    // Public getter to get current turn speed (useful for debugging or UI)
    public float GetCurrentTurnSpeed()
    {
        if (!isTurning) return 0f;
        
        float speedFactor = currentSpeed / maxSpeed;
        float dynamicTurnSpeed = baseTurnSpeed + (baseTurnSpeed * turnSpeedMultiplier * speedFactor);
        return Mathf.Clamp(dynamicTurnSpeed, minTurnSpeed, maxTurnSpeed);
    }
}