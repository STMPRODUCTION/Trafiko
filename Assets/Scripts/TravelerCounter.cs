using UnityEngine;
using System.Collections.Generic;
using System;

public class TravelerCounter : MonoBehaviour
{
    [Header("Traveler Settings")]
    public string travelerLayerName = "Traveler";
    public string carTag = "Car";

    private int travelerLayer;
    private Dictionary<GameObject, float> travelerEntryTimes = new Dictionary<GameObject, float>();
    private float peakWaitTime = 0f;

    // Cached values for performance
    private int cachedCarCount = 0;
    private float cachedAvgWaitTime = 0f;
    private bool needsRecalculation = false;
    
    // Cleanup timing
    private float lastCleanupTime = 0f;
    private const float CLEANUP_INTERVAL = 119.5f;

    // 🔹 Events
    public event Action<TravelerCounter, GameObject> OnCarEntered;
    public event Action<TravelerCounter, GameObject> OnCarExited;

    private void Start()
    {
        travelerLayer = LayerMask.NameToLayer(travelerLayerName);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (IsTravelerOrCar(other.gameObject))
        {
            if (!travelerEntryTimes.ContainsKey(other.gameObject))
            {
                travelerEntryTimes[other.gameObject] = Time.time;
                needsRecalculation = true;

                // 🔹 Fire event if it's a Car
                if (other.CompareTag(carTag))
                {
                    OnCarEntered?.Invoke(this, other.gameObject);
                }
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (IsTravelerOrCar(other.gameObject) && travelerEntryTimes.TryGetValue(other.gameObject, out float entryTime))
        {
            float duration = Time.time - entryTime;
            
            // 🔹 Update peak wait time if this duration is higher
            if (duration > peakWaitTime)
            {
                peakWaitTime = duration;
            }
            
            travelerEntryTimes.Remove(other.gameObject);
            needsRecalculation = true;

            // 🔹 Fire event if it's a Car
            if (other.CompareTag(carTag))
            {
                OnCarExited?.Invoke(this, other.gameObject);
            }
        }
    }

    private bool IsTravelerOrCar(GameObject obj)
    {
        return (obj.layer == travelerLayer) || obj.CompareTag(carTag);
    }

    public int GetTravelerCount()
    {
        CleanupDestroyedObjects();
        return travelerEntryTimes.Count;
    }

    public float GetTravelerDuration(GameObject traveler)
    {
        if (travelerEntryTimes.TryGetValue(traveler, out float entryTime))
        {
            return Time.time - entryTime;
        }
        return 0f;
    }

    public float GetTotalCarWaitTime()
    {
        if (needsRecalculation)
        {
            RecalculateCarStats();
        }
        return cachedAvgWaitTime;
    }

    public int GetCarCount()
    {
        if (needsRecalculation)
        {
            RecalculateCarStats();
        }
        return cachedCarCount;
    }

    private void RecalculateCarStats()
    {
        int carCount = 0;
        float totalTime = 0f;
        float currentTime = Time.time;

        // Use foreach with KeyValuePair to avoid allocations
        foreach (var kvp in travelerEntryTimes)
        {
            if (kvp.Key == null) continue;

            if (kvp.Key.CompareTag(carTag))
            {
                carCount++;
                totalTime += currentTime - kvp.Value;
            }
        }

        cachedCarCount = carCount;
        cachedAvgWaitTime = totalTime;
        needsRecalculation = false;

        // Clean up destroyed objects only when enough time has passed
        if (currentTime - lastCleanupTime >= CLEANUP_INTERVAL)
        {
            CleanupDestroyedObjects();
            lastCleanupTime = currentTime;
        }
    }

    private void CleanupDestroyedObjects()
    {
        // Create a temporary list to store keys to remove
        var keysToRemove = new List<GameObject>();

        foreach (var kvp in travelerEntryTimes)
        {
            if (kvp.Key == null)
            {
                keysToRemove.Add(kvp.Key);
            }
        }

        // Remove the null keys
        foreach (var key in keysToRemove)
        {
            travelerEntryTimes.Remove(key);
        }

        if (keysToRemove.Count > 0)
        {
            needsRecalculation = true;
        }
    }

    public List<GameObject> GetActiveTravelers()
    {
        CleanupDestroyedObjects();
        return new List<GameObject>(travelerEntryTimes.Keys);
    }

    /// <summary>
    /// Returns the peak (maximum) waiting time recorded since the start of the session.
    /// This includes both completed wait times and current ongoing wait times.
    /// </summary>
    /// <returns>The highest waiting time in seconds</returns>
    public float GetPeakWaitingTime()
    {
        float currentPeak = peakWaitTime;
        float currentTime = Time.time;
        
        // Check current active travelers for longer wait times
        foreach (var kvp in travelerEntryTimes)
        {
            if (kvp.Key != null)
            {
                float currentWaitTime = currentTime - kvp.Value;
                if (currentWaitTime > currentPeak)
                {
                    currentPeak = currentWaitTime;
                }
            }
        }
        
        return currentPeak;
    }

    /// <summary>
    /// Resets the peak waiting time counter to zero.
    /// Useful for starting fresh measurements.
    /// </summary>
    public void ResetPeakWaitingTime()
    {
        peakWaitTime = 0f;
    }
}