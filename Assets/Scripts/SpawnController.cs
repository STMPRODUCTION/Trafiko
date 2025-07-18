using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

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

    [Header("Spawn Timing")]
    public float spawnInterval = 3f;
    private float spawnTimer;
    private float timer = 0f;

    [Header("Spawn Distance Check")]
    public float minSpawnDistance = 5f;
    public LayerMask carLayerMask = -1; // Only check specific layers

    [Header("Performance Settings")]
    public int maxCars = 100;
    public float carCountCheckInterval = 1f; // Check car count less frequently
    private float carCountTimer;
    private int currentCarCount = 0;

    [SerializeField] private TrafficStatsLogger statsLogger;

    // Cache spawn data to avoid repeated switch statements
    private SpawnData[] spawnPoints;
    private readonly Collider[] overlapBuffer = new Collider[10]; // Reusable buffer

    [System.Serializable]
    private struct SpawnData
    {
        public Transform spawnPoint;
        public TrafficLight light;
        public string laneID;
    }

    void Start()
    {
        spawnTimer = Random.Range(spawnInterval - 0.5f, spawnInterval + 0.5f);
        carCountTimer = carCountCheckInterval;
        
        // Cache spawn data for better performance
        spawnPoints = new SpawnData[4]
        {
            new SpawnData { spawnPoint = spawnN, light = lightN, laneID = "N" },
            new SpawnData { spawnPoint = spawnS, light = lightS, laneID = "S" },
            new SpawnData { spawnPoint = spawnE, light = lightE, laneID = "E" },
            new SpawnData { spawnPoint = spawnV, light = lightV, laneID = "V" }
        };
    }

    void Update()
    {
        // Check car count less frequently
        carCountTimer -= Time.deltaTime;
        if (carCountTimer <= 0f)
        {
            carCountTimer = carCountCheckInterval;
            UpdateCarCount();
        }

        if (currentCarCount >= maxCars)
        {
            return; // Too many cars, skip spawning
        }

        timer += Time.deltaTime;
        if (timer >= spawnTimer)
        {
            timer = 0f;
            spawnTimer = Random.Range(spawnInterval - 0.5f, spawnInterval + 0.5f);
            SpawnRandomCar();
        }
    }

    void UpdateCarCount()
    {
        // More efficient way to count cars using tags
        GameObject[] allCars = GameObject.FindGameObjectsWithTag("Car");
        currentCarCount = 0;
        
        // Only count cars in current scene
        foreach (GameObject car in allCars)
        {
            if (car.scene == gameObject.scene)
            {
                currentCarCount++;
            }
        }
    }

    bool IsSpawnPointClear(Transform spawnPoint)
    {
        // Use NonAlloc version with buffer and layer mask for better performance
        int hitCount = Physics.OverlapSphereNonAlloc(
            spawnPoint.position, 
            minSpawnDistance, 
            overlapBuffer, 
            carLayerMask
        );
        
        // Check only the colliders that were found
        for (int i = 0; i < hitCount; i++)
        {
            if (overlapBuffer[i].CompareTag("Car"))
            {
                return false; // There's a car too close
            }
        }
        
        return true; // Spawn point is clear
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

    void SpawnRandomCar()
    {
        // Use cached spawn data instead of switch statement
        int direction = Random.Range(0, 4);
        SpawnData spawnData = spawnPoints[direction];

        // Check if spawn point is clear before getting prefab
        if (!IsSpawnPointClear(spawnData.spawnPoint))
        {
            return; // Don't spawn if there's a car too close
        }

        GameObject selectedPrefab = GetRandomCarPrefab();
        if (selectedPrefab == null) return;

        GameObject car = Instantiate(selectedPrefab, spawnData.spawnPoint.position, spawnData.spawnPoint.rotation);
    
        // Move the car to the same scene as the spawner
        SceneManager.MoveGameObjectToScene(car, gameObject.scene);
        
        car.tag = "Car";
    
        CarController carCtrl = car.GetComponent<CarController>();
        carCtrl.statsLogger = statsLogger;
        carCtrl.targetLight = spawnData.light;
        carCtrl.laneID = spawnData.laneID;

        // Update car count immediately when spawning
        currentCarCount++;
        statsLogger.ReportCarSpawn(spawnData.laneID);
    }

    void SpawnCarAtLane(int laneIndex)
    {
        if (laneIndex < 0 || laneIndex >= spawnPoints.Length) return;
        
        SpawnData spawnData = spawnPoints[laneIndex];

        // Check if spawn point is clear before getting prefab
        if (!IsSpawnPointClear(spawnData.spawnPoint))
        {
            return; // Don't spawn if there's a car too close
        }

        GameObject selectedPrefab = GetRandomCarPrefab();
        if (selectedPrefab == null) return;

        GameObject car = Instantiate(selectedPrefab, spawnData.spawnPoint.position, spawnData.spawnPoint.rotation);
        SceneManager.MoveGameObjectToScene(car, gameObject.scene);
        car.tag = "Car";
        
        CarController carCtrl = car.GetComponent<CarController>();
        carCtrl.targetLight = spawnData.light;
        carCtrl.laneID = spawnData.laneID;

        currentCarCount++;
        statsLogger.ReportCarSpawn(spawnData.laneID);
    }

    void SpawnCarAllDirections()
    {
        for (int i = 0; i < spawnPoints.Length; i++)
        {
            SpawnCarAtLane(i);
        }
    }
}