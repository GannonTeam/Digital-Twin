using UnityEngine;
using TMPro;
using UnityEngine.UI;

// NOTE: This script must be attached to each of your 40 individual printer panels.
public class PrinterUIHandler : MonoBehaviour
{
    // --- Assign in Inspector ---
    // This ID must match the PrinterId field sent by the SignalR backend
    [Header("Printer Identifier")]
    [Tooltip("Must match the PrinterId from the backend.")]
    public string PrinterId; 

    [Header("UI References (Assign Text Components)")]
    public TextMeshProUGUI statusText;
    public TextMeshProUGUI progressText;
    // public Slider progressSlider; <-- Removed
    public TextMeshProUGUI bedTempText;
    public TextMeshProUGUI nozzleTempText;
    public Image statusLight; // Visual indicator for status

    /// <summary>
    /// Updates all UI elements for this specific printer based on new data.
    /// This method is called directly by the DashboardManager on the main thread.
    /// </summary>
    /// <param name="data">The latest PrinterData object for this printer.</param>
    public void DisplayData(PrinterData data)
    {
        // 1. Update text fields and values
        statusText.text = data.Status;
        
        // Since Progress, BedTemp, and NozzleTemp are 'double', ensure consistent formatting.
        float progressValue = (float)data.Progress;
        float bedTempValue = (float)data.BedTemp;
        float nozzleTempValue = (float)data.NozzleTemp;

        progressText.text = $"{progressValue:F1}%";
        bedTempText.text = $"{bedTempValue:F1}°C";
        nozzleTempText.text = $"{nozzleTempValue:F1}°C";

        // 2. The progress slider update line was removed here.

        // 3. Update Status Visuals
        UpdateStatusVisuals(data.Status);
    }

    private void UpdateStatusVisuals(string status)
    {
        Color statusColor = Color.gray; // Default

        switch (status.ToLower())
        {
            case "printing":
                statusColor = Color.green;
                break;
            case "warming up":
            case "pausing":
                statusColor = Color.yellow;
                break;
            case "error":
            case "jammed":
                statusColor = Color.red;
                break;
            case "idle":
                statusColor = Color.cyan;
                break;
        }
        if (statusLight != null)
        {
            statusLight.color = statusColor;
        }
    }
}
