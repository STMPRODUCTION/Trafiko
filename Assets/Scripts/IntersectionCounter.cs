using UnityEngine;

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

    public int GetCarsWaitingInIntersection()
    {
        // Sum up cars in all entrance triggers
        int total = 0;
        total += N_extreme_entrace.GetCarCount();
        total += N_centre_entrace.GetCarCount();
        total += S_extreme_entrace.GetCarCount();
        total += S_centre_entrace.GetCarCount();
        total += E_extreme_entrace.GetCarCount();
        total += E_centre_entrace.GetCarCount();
        total += V_extreme_entrace.GetCarCount();
        total += V_centre_entrace.GetCarCount();
        return total;
    }

    private void Update()
    {
        count = GetCarsWaitingInIntersection();
        avgTime = GetAvrageWaitingTime();
    }

    public int GetCarsInIntersection()
    {
        // Value updated by listening to TravelerCounter events
        return carsInIntersection;
    }

    public float GetAvrageWaitingTime()
    {
        // Average of averages across all entrance triggers
        float total = 0f;
        int counter = 0;

        TravelerCounter[] entrances = {
            N_extreme_entrace, N_centre_entrace,
            S_extreme_entrace, S_centre_entrace,
            E_extreme_entrace, E_centre_entrace,
            V_extreme_entrace, V_centre_entrace
        };

        foreach (var entrance in entrances)
        {
            float avg = entrance.GetAverageCarWaitTime();
            if (avg > 0f)
            {
                total += avg;
                counter++;
            }
        }

        return counter > 0 ? total / counter : 0f;
    }

    private void OnEnable()
    {
        // Subscribe to TravelerCounter events for cars entering/exiting
        SubscribeToCounter(N_extreme_entrace);
        SubscribeToCounter(N_centre_entrace);
        SubscribeToCounter(N_extreme_exit);
        SubscribeToCounter(N_centre_exit);
        SubscribeToCounter(S_extreme_entrace);
        SubscribeToCounter(S_centre_entrace);
        SubscribeToCounter(S_extreme_exit);
        SubscribeToCounter(S_centre_exit);
        SubscribeToCounter(E_extreme_entrace);
        SubscribeToCounter(E_centre_entrace);
        SubscribeToCounter(E_extreme_exit);
        SubscribeToCounter(E_centre_exit);
        SubscribeToCounter(V_extreme_entrace);
        SubscribeToCounter(V_centre_entrace);
        SubscribeToCounter(V_extreme_exit);
        SubscribeToCounter(V_centre_exit);
    }

    private void SubscribeToCounter(TravelerCounter counter)
    {
        if (counter == null) return;
        counter.OnCarEntered += HandleCarEntered;
        counter.OnCarExited += HandleCarExited;
    }

    private void HandleCarEntered(TravelerCounter counter, GameObject car)
    {
        // If entered an entrance, not yet in intersection
        // If entered an exit, it has finished crossing
        if (IsExitCounter(counter))
        {
            carsInIntersection--;
            if (carsInIntersection < 0) carsInIntersection = 0; // safety clamp
        }
    }

    public int GetCarsAtEntrance(int id)
    {
        TravelerCounter[] entrances = {
            N_extreme_entrace, N_centre_entrace,
            S_extreme_entrace, S_centre_entrace,
            E_extreme_entrace, E_centre_entrace,
            V_extreme_entrace, V_centre_entrace
        };

        if (id < 0 || id >= entrances.Length || entrances[id] == null)
            return 0;

        return entrances[id].GetCarCount();
    }

    private void HandleCarExited(TravelerCounter counter, GameObject car)
    {
        // If exited an entrance, car has entered intersection
        if (IsEntranceCounter(counter))
        {
            carsInIntersection++;
        }
    }

    private bool IsEntranceCounter(TravelerCounter counter)
    {
        return counter == N_extreme_entrace || counter == N_centre_entrace ||
               counter == S_extreme_entrace || counter == S_centre_entrace ||
               counter == E_extreme_entrace || counter == E_centre_entrace ||
               counter == V_extreme_entrace || counter == V_centre_entrace;
    }

    private bool IsExitCounter(TravelerCounter counter)
    {
        return counter == N_extreme_exit || counter == N_centre_exit ||
               counter == S_extreme_exit || counter == S_centre_exit ||
               counter == E_extreme_exit || counter == E_centre_exit ||
               counter == V_extreme_exit || counter == V_centre_exit;
    }
}
