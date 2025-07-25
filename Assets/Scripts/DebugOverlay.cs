using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DebugOverlay : MonoBehaviour
{
    [Header("UI References")]
    public GameObject overlayPanel;
    public Slider timescaleSlider;
    public TextMeshProUGUI timescaleValueText;
    public TextMeshProUGUI variablesText;
    
    [Header("Debug Variables")]
    public float exampleFloat = 42.5f;
    public int exampleInt = 100;
    public string exampleString = "Debug Mode";
    public bool exampleBool = true;
    
    private bool isOverlayVisible = false;
    private float updateTimer = 0f;
    private float updateInterval = 0.1f; // Update variables display every 0.1 seconds
    public TrafficStatsLogger statsLogger;

    public GameObject Props;
    
    void Start()
    {
        SetupTimescaleSlider();
        SetupOverlay();
        UpdateVariablesDisplay();
    }
    
    void Update()
    {
        HandleInput();
        UpdateTimer();
        
        if (isOverlayVisible)
        {
            UpdateTimescaleDisplay();
        }
    }
    
    void HandleInput()
    {
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            ToggleOverlay();
        }
    }
    
    void UpdateTimer()
    {
        updateTimer += Time.unscaledDeltaTime;
        if (updateTimer >= updateInterval && isOverlayVisible)
        {
            UpdateVariablesDisplay();
            updateTimer = 0f;
        }
    }
    
    void SetupTimescaleSlider()
    {
        if (timescaleSlider != null)
        {
            timescaleSlider.value = statsLogger.simulationTimeScale;
        }
    }
    
    void SetupOverlay()
    {
        if (overlayPanel != null)
        {
            overlayPanel.SetActive(false);
            isOverlayVisible = false;
        }
    }
    public void onTimeScaleChanged(float value)
    {
        statsLogger.simulationTimeScale = value;
    }    
    void ToggleOverlay()
    {
        isOverlayVisible = !isOverlayVisible;
        
        if (overlayPanel != null)
        {
            overlayPanel.SetActive(isOverlayVisible);
        }
        
        if (isOverlayVisible)
        {
            UpdateVariablesDisplay();
            UpdateTimescaleDisplay();
        }
    }
     public void ToggleScenery(bool toggle)
    {
      Props.SetActive(toggle);
    }
    
    void UpdateTimescaleDisplay()
    {
        if (timescaleValueText != null)
        {
            timescaleValueText.text = $"Timescale: {Time.timeScale:F2}x";
        }
    }
    
    void UpdateVariablesDisplay()
    {
        if (variablesText != null)
        {
            string variables = "=== DEBUG ===\n\n";
            variables += $"Frame Rate: {(1f / Time.unscaledDeltaTime):F0} FPS\n";
            variables += $"Delta Time: {Time.deltaTime:F4}s\n";
            variables += $"Time Since Start: {Time.time:F2}s\n\n";
            
            variables += "=== INTERSECTION EFFICIENCY===\n\n";
            variables += $"Avrage delay: {statsLogger.avrageWaitTime:F2}\n";
            variables += $"Num of cars passed: {statsLogger.carsReported}\n";
            variables += $"Avrage speed: {statsLogger.avgCarSpeed}\n";
            variables += $"North lanes queue : {statsLogger.GetCarsOnLane("N") + statsLogger.GetCarsOnLane("NLeft")}\n";
            variables += $"South lanes queue : {statsLogger.GetCarsOnLane("S")+ statsLogger.GetCarsOnLane("SLeft")}\n";
            variables += $"East lanes queue : {statsLogger.GetCarsOnLane("E")+ statsLogger.GetCarsOnLane("ELeft")}\n";
            variables += $"West lanes queue : {statsLogger.GetCarsOnLane("V")+ statsLogger.GetCarsOnLane("VLeft")}\n";
            variablesText.text = variables;
        }
    }
    
    // Method to add custom variables dynamically
    public void SetCustomVariable(string name, object value)
    {
        // You can expand this to handle dynamic variables
        // For now, it's a placeholder for future functionality
        Debug.Log($"Custom Variable - {name}: {value}");
    }
    
    void OnValidate()
    {
        // Clamp values in inspector
        exampleFloat = Mathf.Clamp(exampleFloat, -1000f, 1000f);
        exampleInt = Mathf.Clamp(exampleInt, 0, 99999);
    }
}