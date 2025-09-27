using UnityEngine;

public class Attach : MonoBehaviour
{
    [Header("Camera Settings")]
    public Camera mainCamera; // Assign your MainCamera here

    [Tooltip("Check this to attach the camera to a random Car. Uncheck to reset.")]
    public bool attachCamera = false;

    private bool alreadyAttached = false;

    void Update()
    {
        if (attachCamera && !alreadyAttached)
        {
            AttachToRandomCar();
            alreadyAttached = true;
        }

        if (!attachCamera && alreadyAttached)
        {
            ResetCamera();
            alreadyAttached = false;
        }
    }

    void AttachToRandomCar()
    {
        GameObject[] cars = GameObject.FindGameObjectsWithTag("Car");

        if (cars.Length == 0)
        {
            Debug.LogWarning("No objects with tag 'Car' found!");
            return;
        }

        // Pick a random car
        GameObject randomCar = cars[Random.Range(0, cars.Length)];

        if (mainCamera != null)
        {
            // Set the camera as a child of the car
            mainCamera.transform.SetParent(randomCar.transform);

            // Apply the local position and rotation
            mainCamera.transform.localPosition = new Vector3(0f, 1.509997f, -1.57f);
            mainCamera.transform.localRotation = Quaternion.Euler(5.954f, 0f, 0f);

            Debug.Log($"Camera attached to {randomCar.name}");
        }
        else
        {
            Debug.LogError("MainCamera is not assigned in the Inspector!");
        }
    }

    void ResetCamera()
    {
        if (mainCamera != null)
        {
            // Detach from parent
            mainCamera.transform.SetParent(null);

            // Reset position & rotation to world origin
            mainCamera.transform.position = Vector3.zero;
            mainCamera.transform.rotation = Quaternion.identity;

            Debug.Log("Camera reset to world origin (0,0,0).");
        }
    }
}
