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
    public bool isClockwise = true; // Add this missing variable

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
    [SerializeField] private float targetRotationY;
    [SerializeField] private float initialRotationY;
    [SerializeField] private bool turnStarted = false;
    [SerializeField] private string originalLaneID;

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

    // Turn calculation variables
    [SerializeField]private float totalRotation = 0f;
    [SerializeField]private float initialAngle;
    [SerializeField]private Vector3 initialPosition;
    [SerializeField]private Vector3 previousPosition;
    [SerializeField]private float targetTotalRotation = 90f; // for 90-degree turn
    [SerializeField]private float turnRadius;
    [SerializeField]public Vector3 turnPoint;

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
        if(!isGoingRight)
            isGoingRight = Random.Range(0f, 1f) < rightTurnChance;
        
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
        // Cache deltaTime to avoid multiple calls
        float deltaTime = Time.deltaTime;
        accelerationTimesDelta = acceleration * deltaTime;
        decelerationTimesDelta = acceleration * deceleration * deltaTime;

        // Handle turning after passing the light
        if (isTurning)
        {
            HandleTurn(deltaTime);
            return;
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

    public void PrepareForRotation(bool clockwise = true)
    {
        isClockwise = clockwise;
        initialRotationY = transform.eulerAngles.y;
        if(isClockwise)
        {
            targetRotationY = initialRotationY + 90f;
            //Debug.LogWarning(initialRotationY); // Target is 90 degrees to the right
            targetTotalRotation = 90f;
        }
        else
        {
            targetRotationY = initialRotationY - 90f; // Target is 90 degrees to the left
            targetTotalRotation = -90f;
            //Debug.LogWarning(initialRotationY);
        }
    }

    public void StartRightTurn()
    {
        isTurning = true;
        totalRotation = 0f;
        initialPosition = transform.position;
        previousPosition = transform.position;
        //isClockwise = clockwise; // Store the direction
        
        // Calculate initial angle relative to turn point
        Vector3 toInitialPos = initialPosition - turnPoint;
        initialAngle = Mathf.Atan2(toInitialPos.z, toInitialPos.x) * Mathf.Rad2Deg;
        
        // Calculate the radius for this turn
        turnRadius = Vector3.Distance(initialPosition, turnPoint);
        
        // Prepare target rotation
        //PrepareForRotation();
        
        //Debug.Log($"Starting turn: clockwise={isClockwise}, initialAngle={initialAngle}, turnRadius={turnRadius}");
    }
    public float linearSpeed = 5f;
    void HandleTurn(float deltaTime)
    {
        // Calculate angular speed based on current linear speed
        float angularSpeed = linearSpeed / turnRadius; // radians per second
        float angularSpeedDegrees = angularSpeed * Mathf.Rad2Deg;
        
        // Apply turn speed limits
        angularSpeedDegrees = Mathf.Clamp(angularSpeedDegrees, minTurnSpeed, maxTurnSpeed);
        
        float rotationThisFrame = angularSpeedDegrees * deltaTime;
        totalRotation += rotationThisFrame;

        // Calculate direction multiplier
        // For clockwise (right turn): positive rotation
        // For counter-clockwise (left turn): negative rotation
        float directionMultiplier = isClockwise ? -1f : 1f;  //1f for left
        float currentAngle = initialAngle + (totalRotation * directionMultiplier);
        
        // Calculate new position on the circular path
        Vector3 newPosition = turnPoint + new Vector3(
            Mathf.Cos(currentAngle * Mathf.Deg2Rad) * turnRadius,
            initialPosition.y, // Keep same Y height
            Mathf.Sin(currentAngle * Mathf.Deg2Rad) * turnRadius
        );
        
        transform.position = newPosition;

        // Calculate movement direction for proper car orientation
        Vector3 movementDirection = (newPosition - previousPosition).normalized;
        
        if (movementDirection.magnitude > 0.01f)
        {
            // Smoothly rotate the car to face the movement direction
            Quaternion targetRotation = Quaternion.LookRotation(movementDirection, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 10f * deltaTime);
        }
        
        previousPosition = newPosition;

        // Check if turn is complete
        if (totalRotation >=  Mathf.Abs(targetTotalRotation))
        {
            CompleteTurn();
        }
    }
    
    void CompleteTurn()
    {
        isTurning = false;
        
        // Snap to final rotation for precision
        transform.rotation = Quaternion.Euler(transform.eulerAngles.x, targetRotationY, transform.eulerAngles.z);
        // Change lane after completing the turn
        ChangeLaneAfterTurn();
        
        //Debug.LogWarning($"Turn completed. New lane: {laneID}");
    }
    
    void ChangeLaneAfterTurn()
    {
        string newLaneID = GetNewLaneIDAfterTurn(originalLaneID);
        
        if (newLaneID != laneID)
        {
            string previousLaneID = laneID;
            laneID = newLaneID;
            
            // Log the lane change
            if (statsLogger != null)
            {
                //Debug.Log($"Car changed lane from {previousLaneID} to {laneID} after turn");
                statsLogger.ReportLaneChange(previousLaneID, laneID);
            }
        }
    }

    string GetNewLaneIDAfterTurn(string currentLaneID)
    {
        // Map lane changes after turns
        // Adjust these mappings based on your intersection layout
        switch (currentLaneID.ToUpper())
        {
            case "N": // North lane
                return isClockwise ? "E" : "V"; // Right turn -> East, Left turn -> West
            case "S": // South lane  
                return isClockwise ? "V" : "E"; // Right turn -> West, Left turn -> East
            case "E": // East lane
                return isClockwise ? "S" : "N"; // Right turn -> South, Left turn -> North
            case "V": // West lane
                return isClockwise ? "N" : "S";
            case "NLEFT": // North lane
                return "V";
            case "SLEFT": // South lane  
                return "E"; // Right turn -> West, Left turn -> East
            case "ELEFT": // East lane
                return "N"; // Right turn -> South, Left turn -> North
            case "VLEFT": // West lane
                return "S"; // Right turn -> North, Left turn -> South // Right turn -> North, Left turn -> South
            default:
                return currentLaneID; // Return original if no mapping found
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
                angerScore = angerGrowthRate * waitTimeForAnger;
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
                StartRightTurn(); // true for clockwise (right turn)
                turnStarted = true;
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

        // Only move forward when not turning
        if (!isTurning)
            transform.Translate(Vector3.forward * currentSpeed * deltaTime);
    }

    void OnTriggerEnter(Collider other)
    {
        if (isDestroyed) return;
        
        if (!laneID.EndsWith("Left"))
        {
            // Left lane logic
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
                    DestroyCar(timeToDestroy);
                }
            }
        }
        else
        {
            // Non-left lane logic
            CarDestroyHelper(other);
        }
    }
    [SerializeField] private float timeToDestroy = 5f;
    void CarDestroyHelper(Collider other)
    {
        string triggerLaneID = laneID.Replace("Left", "");
        if (!hasPassedLight && other.CompareTag($"IntersectionTrigger{triggerLaneID}"))
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
            DestroyCar(timeToDestroy);
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
                DestroyCar(timeToDestroy);
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
        
        float linearSpeed = Mathf.Max(currentSpeed, slowestAllowedSpeed);
        float angularSpeed = linearSpeed / turnRadius;
        float angularSpeedDegrees = angularSpeed * Mathf.Rad2Deg;
        return Mathf.Clamp(angularSpeedDegrees, minTurnSpeed, maxTurnSpeed);
    }
}