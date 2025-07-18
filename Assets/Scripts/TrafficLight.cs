using UnityEngine;

public class TrafficLight : MonoBehaviour
{
    public bool isGreen = false;

    [Header("Auto Switch")]
    public float greenDuration = 5f;
    public float redDuration = 5f;

    //private float timer = 0f;
    private Renderer rend;

    void Start()
    {
        rend = GetComponent<Renderer>();
        UpdateColor();
    }

    /*void Update()
    {
        timer += Time.deltaTime;

        if (isGreen && timer >= greenDuration)
        {
            isGreen = false;
            timer = 0f;
            UpdateColor();
        }
        else if (!isGreen && timer >= redDuration)
        {
            isGreen = true;
            timer = 0f;
            UpdateColor();
        }
    }*/

    void UpdateColor()
    {
        if (rend == null) return;

        if (isGreen)
            rend.material.color = Color.green;
        else
            rend.material.color = Color.red;
    }
    public void SetGreen(bool green)
{
    isGreen = green;
    UpdateColor();
    // Add your logic to update visuals/colliders if needed
}

}
