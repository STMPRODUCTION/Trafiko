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
                Debug.Log($"[{Time.time:F2}] {other.name} entered {gameObject.name}.");

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
        if (IsTravelerOrCar(other.gameObject))
        {
            if (travelerEntryTimes.TryGetValue(other.gameObject, out float entryTime))
            {
                float duration = Time.time - entryTime;
                Debug.Log($"[{Time.time:F2}] {other.name} exited {gameObject.name} after {duration:F2}s.");
                travelerEntryTimes.Remove(other.gameObject);

                // 🔹 Fire event if it's a Car
                if (other.CompareTag(carTag))
                {
                    OnCarExited?.Invoke(this, other.gameObject);
                }
            }
        }
    }

    private bool IsTravelerOrCar(GameObject obj)
    {
        return (obj.layer == travelerLayer) || obj.CompareTag(carTag);
    }

    public int GetTravelerCount()
    {
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

    public float GetAverageCarWaitTime()
    {
        int carCount = 0;
        float totalTime = 0f;

        foreach (var kvp in travelerEntryTimes)
        {
            if (kvp.Key.CompareTag(carTag))
            {
                carCount++;
                totalTime += Time.time - kvp.Value;
            }
        }

        return carCount == 0 ? 0f : totalTime / carCount;
    }

    public int GetCarCount()
    {
        int count = 0;
        foreach (var kvp in travelerEntryTimes)
        {
            if (kvp.Key.CompareTag(carTag)) count++;
        }
        return count;
    }

    public List<GameObject> GetActiveTravelers()
    {
        return new List<GameObject>(travelerEntryTimes.Keys);
    }
}
