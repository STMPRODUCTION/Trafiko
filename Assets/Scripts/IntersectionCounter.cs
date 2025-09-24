using UnityEngine;
using System;

public class IntersectionCounter : MonoBehaviour
{   
    [SerializeField] private TravelerCounter N_extreme_entrace;
    [SerializeField] private TravelerCounter N_centre_entrace;
    [SerializeField] private TravelerCounter N_extreme_exit;
    [SerializeField] private TravelerCounter N_centre_exit;

    [SerializeField] private TravelerCounter S_extreme_entrace;
    [SerializeField] private TravelerCounter S_centre_entrace;
    [SerializeField] private TravelerCounter S_extreme_exit;
    [SerializeField] private TravelerCounter S_centre_exit;

    [SerializeField] private TravelerCounter E_extreme_entrace;
    [SerializeField] private TravelerCounter E_centre_entrace;
    [SerializeField] private TravelerCounter E_extreme_exit;
    [SerializeField] private TravelerCounter E_centre_exit;

    [SerializeField] private TravelerCounter V_extreme_entrace;
    [SerializeField] private TravelerCounter V_centre_entrace;
    [SerializeField] private TravelerCounter V_extreme_exit;
    [SerializeField] private TravelerCounter V_centre_exit;

    public int count;
    public float avgTime;

    private int carsInIntersection = 0;
    
    // Cached arrays to avoid creating new ones each frame
    private TravelerCounter[] entrances;
    private TravelerCounter[] exits;
    private TravelerCounter[] allCounters;

    public event Action OnCarLeft;

    private void Awake()
    {
        // Initialize cached arrays once
        entrances = new TravelerCounter[]
        {
            N_extreme_entrace, N_centre_entrace,
            S_extreme_entrace, S_centre_entrace,
            E_extreme_entrace, E_centre_entrace,
            V_extreme_entrace, V_centre_entrace
        };

        exits = new TravelerCounter[]
        {
            N_extreme_exit, N_centre_exit,
            S_extreme_exit, S_centre_exit,
            E_extreme_exit, E_centre_exit,
            V_extreme_exit, V_centre_exit
        };

        allCounters = new TravelerCounter[]
        {
            N_extreme_entrace, N_centre_entrace, N_extreme_exit, N_centre_exit,
            S_extreme_entrace, S_centre_entrace, S_extreme_exit, S_centre_exit,
            E_extreme_entrace, E_centre_entrace, E_extreme_exit, E_centre_exit,
            V_extreme_entrace, V_centre_entrace, V_extreme_exit, V_centre_exit
        };
    }

    public int GetCarsWaitingInIntersection()
    {
        int total = 0;
        for (int i = 0; i < entrances.Length; i++)
        {
            if (entrances[i] != null)
                total += entrances[i].GetCarCount();
        }
        return total;
    }

    private void Update()
    {
        count = GetCarsWaitingInIntersection();
        avgTime = GetTotalWaitingTime();
    }

    public int GetCarsInIntersection()
    {
        return carsInIntersection;
    }

    public float GetTotalWaitingTime()
    {
        float total = 0f;
        int counter = 0;

        for (int i = 0; i < entrances.Length; i++)
        {
            float avg = entrances[i].GetTotalCarWaitTime();
            total += avg;

        }
        return total;
    }
    private void OnEnable()
    {
        // Subscribe to all counters using cached array
        for (int i = 0; i < allCounters.Length; i++)
        {
            SubscribeToCounter(allCounters[i]);
        }
    }

    private void OnDisable()
    {
        // Unsubscribe from all counters to prevent memory leaks
        for (int i = 0; i < allCounters.Length; i++)
        {
            UnsubscribeFromCounter(allCounters[i]);
        }
    }

    private void SubscribeToCounter(TravelerCounter counter)
    {
        if (counter == null) return;
        counter.OnCarEntered += HandleCarEntered;
        counter.OnCarExited += HandleCarExited;
    }

    private void UnsubscribeFromCounter(TravelerCounter counter)
    {
        if (counter == null) return;
        counter.OnCarEntered -= HandleCarEntered;
        counter.OnCarExited -= HandleCarExited;
    }

    private void HandleCarEntered(TravelerCounter counter, GameObject car)
    {
        if (IsExitCounter(counter))
        {
            carsInIntersection--;
            if (carsInIntersection < 0) carsInIntersection = 0;
            OnCarLeft?.Invoke();
        }
    }

    public int GetCarsAtEntrance(int id)
    {
        if (id < 0 || id >= entrances.Length || entrances[id] == null)
            return 0;

        return entrances[id].GetCarCount();
    }

    public float GetWaitTimeAtEntrance(int id)
    {
        if (id < 0 || id >= entrances.Length || entrances[id] == null)
            return 0;

        return entrances[id].GetTotalCarWaitTime();
    }

    private void HandleCarExited(TravelerCounter counter, GameObject car)
    {
        if (IsEntranceCounter(counter))
        {
            carsInIntersection++;
        }
    }

    private bool IsEntranceCounter(TravelerCounter counter)
    {
        for (int i = 0; i < entrances.Length; i++)
        {
            if (entrances[i] == counter) return true;
        }
        return false;
    }

    private bool IsExitCounter(TravelerCounter counter)
    {
        for (int i = 0; i < exits.Length; i++)
        {
            if (exits[i] == counter) return true;
        }
        return false;
    }
}