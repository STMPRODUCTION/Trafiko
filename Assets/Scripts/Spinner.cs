using UnityEngine;

public class Spinner : MonoBehaviour
{
    [Header("Rotation Settings")]
    [Tooltip("Rotation speed in degrees per second.")]
    public float rotationSpeed = 45f; // Default speed

    void Update()
    {
        // Rotate the object around its Y axis at the given speed
        transform.Rotate(0f, rotationSpeed * Time.deltaTime, 0f);
    }
}
