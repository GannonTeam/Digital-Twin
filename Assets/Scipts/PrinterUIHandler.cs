using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Globalization;
using System; 

/// <summary>
/// Printer UI handler for the single-panel dashboard.
/// - Supports binding to a printer id at runtime via SetPrinterId(...).
/// - Displays the GameObject Name > devId (Simplified Fallback).
/// - When an id is set it can ask DashboardManager to show that printer and optionally ask the PollingClient to RequestSingle immediately.
/// - Smooths numeric values and shows labeled fields (Status: Running, Progress: 23.5%, etc.)
/// </summary>
public class PrinterUIHandler : MonoBehaviour
{
    [Header("UI References (Assign Text Components)")]
    [Tooltip("Label showing the currently bound Printer Id (e.g. 'PRN001').")]
    public TextMeshProUGUI printerIdLabel;

    [Tooltip("Optional editable input for entering a PrinterId (on EndEdit it will bind).")]
    public TMP_InputField printerIdInput;

    [Tooltip("Status label text (e.g. 'Status: Running')")]
    public TextMeshProUGUI statusText;

    [Tooltip("Progress label text (e.g. 'Progress: 23.5%')")]
    public TextMeshProUGUI progressText;

    [Tooltip("Optional progress slider (visual)")]
    public Slider progressSlider;

    [Tooltip("Bed temperature label (e.g. 'Bed: 60.0°C')")]
    public TextMeshProUGUI bedTempText;

    [Tooltip("Nozzle temperature label (e.g. 'Nozzle: 210.0°C')")]
    public TextMeshProUGUI nozzleTempText;

    [Tooltip("Small color indicator for status (Image)")]
    public Image statusLight;

    [Header("Label Prefixes (customizable)")]
    public string statusPrefix = "Status: ";
    public string progressPrefix = "Progress: ";
    public string bedTempPrefix = "Bed: ";
    public string nozzleTempPrefix = "Nozzle: ";

    [Header("Smoothing")]
    [Tooltip("Higher = snappier updates")]
    public float progressLerp = 8f;
    public float tempLerp = 6f;

    [Header("Behavior")]
    [Tooltip("When SetPrinterId is called, call DashboardManager.ShowPrinter(id) to bind and request data.")]
    public bool showPrinterViaManagerOnSet = true;

    [Tooltip("When SetPrinterId is called, also call PrinterPollingClient.RequestSingle(id) if a poller exists.")]
    public bool requestSingleOnSet = true;

    // internal state
    private PrinterState lastState;
    private float displayedProgress;
    private float displayedNozzle;
    private float displayedBed;
    private bool initialized;

    // Stores the name of the GameObject that triggered this panel open (The new primary name source)
    private string fallbackGameObjectName = string.Empty; 

    // the current bound printer id (empty if unbound)
    public string CurrentPrinterId { get; private set; } = string.Empty;

    void Awake()
    {
        if (printerIdLabel != null && string.IsNullOrEmpty(printerIdLabel.text))
            printerIdLabel.text = "--";

        if (printerIdInput != null)
        {
            if (printerIdLabel != null && !string.IsNullOrEmpty(printerIdLabel.text) && printerIdLabel.text != "--")
                printerIdInput.text = printerIdLabel.text;

            printerIdInput.onEndEdit.AddListener(OnPrinterIdInputEndEdit);
        }
    }

    void OnDestroy()
    {
        if (printerIdInput != null)
            printerIdInput.onEndEdit.RemoveListener(OnPrinterIdInputEndEdit);
    }

    /// <summary>
    /// Binds the panel to a printer id and sets a fallback name.
    /// </summary>
    public void SetPrinterId(string printerId, string gameObjectName, PrinterState initialState = null, bool triggerRequest = true)
    {
        CurrentPrinterId = printerId ?? string.Empty;
        fallbackGameObjectName = gameObjectName ?? string.Empty; // Store the fallback name
        lastState = null;
        initialized = false;

        // Set label initially using the best available name (for immediate feedback)
        string initialLabel = GetBestAvailableName(initialState, CurrentPrinterId, fallbackGameObjectName);
        if (printerIdLabel != null)
            printerIdLabel.text = initialLabel;

        if (printerIdInput != null && printerIdInput.text != CurrentPrinterId)
            printerIdInput.text = CurrentPrinterId;

        if (string.IsNullOrEmpty(CurrentPrinterId))
        {
            ClearDisplay();
            return;
        }

        if (initialState != null)
        {
            ApplyInitialState(initialState);
        }
        else
        {
            ClearDisplayAsLoading();
        }

        // --- FIX FOR STACK OVERFLOW: Logic remains the same ---
        if (triggerRequest)
        {
            if (requestSingleOnSet)
            {
                var poller = FindObjectOfType<PrinterPollingClient>();
                if (poller != null)
                {
                    poller.RequestSingle(CurrentPrinterId);
                }
            }
        }
    }
    
    public void SetPrinterId(string printerId, PrinterState initialState = null, bool triggerRequest = true)
    {
        SetPrinterId(printerId, null, initialState, triggerRequest);
    }

    /// <summary>
    /// Helper to determine the best display name based on available data.
    /// SIMPLIFIED: Always prioritize the GameObject name.
    /// </summary>
    private string GetBestAvailableName(PrinterState state, string currentId, string fallbackName)
    {
        // Tier 1: GameObject Fallback Name (The new and constant priority)
        if (!string.IsNullOrEmpty(fallbackName))
        {
            return fallbackName;
        }
        
        // Tier 2: Raw ID (Last resort if no GameObject name was provided for some reason)
        if (!string.IsNullOrEmpty(currentId))
        {
            return currentId;
        }

        return "--";
    }

    /// <summary>
    /// Called when user finishes editing the printerIdInput field (if present).
    /// </summary>
    private void OnPrinterIdInputEndEdit(string newText)
    {
        if (string.IsNullOrWhiteSpace(newText))
        {
            SetPrinterId(string.Empty, null, false);
            return;
        }

        SetPrinterId(newText.Trim(), null, true);
    }

    private void ApplyInitialState(PrinterState s)
    {
        lastState = s;
        // Access nested fields via 'reported'
        displayedProgress = (float)s.reported.progressPct;
        displayedNozzle = (float)s.reported.nozzleC;
        displayedBed = (float)s.reported.bedC;
        initialized = true;

        // Apply best name now that state data is available (uses simplified logic)
        if (printerIdLabel != null)
        {
            printerIdLabel.text = GetBestAvailableName(s, CurrentPrinterId, fallbackGameObjectName);
        }

        // update immediate labeled texts
        // Access nested state field
        if (statusText != null) statusText.text = statusPrefix + ToTitleCaseSafe(s.reported.state);
        
        if (progressText != null) progressText.text = progressPrefix + $"{displayedProgress:F1}%";
        if (progressSlider != null) progressSlider.value = Mathf.Clamp01(displayedProgress / 100f);
        if (bedTempText != null) bedTempText.text = bedTempPrefix + $"{displayedBed:F1}°C";
        if (nozzleTempText != null) nozzleTempText.text = nozzleTempPrefix + $"{displayedNozzle:F1}°C"; 

        // Access nested state field
        UpdateStatusLight(s.reported.state);
    }

    /// <summary>
    /// Main update entrypoint called by DashboardManager when a new state arrives.
    /// Must be called on the main thread.
    /// </summary>
    public void DisplayState(PrinterState state)
    {
        if (state == null || string.IsNullOrEmpty(state.devId)) return;
        if (string.IsNullOrEmpty(CurrentPrinterId) || state.devId != CurrentPrinterId) return;

        lastState = state;

        if (!initialized)
        {
            // Access nested fields via 'reported'
            displayedProgress = (float)state.reported.progressPct;
            displayedNozzle = (float)state.reported.nozzleC;
            displayedBed = (float)state.reported.bedC;
            initialized = true;
        }

        // Update name label (uses simplified logic)
        if (printerIdLabel != null)
        {
            printerIdLabel.text = GetBestAvailableName(state, CurrentPrinterId, fallbackGameObjectName);
        }

        // immediate text for status (readable)
        // Access nested state field
        if (statusText != null) statusText.text = statusPrefix + ToTitleCaseSafe(state.reported.state);
        
        // Access nested state field
        UpdateStatusLight(state.reported.state);

        // numeric fields smoothed in Update()
    }

    void Update()
    {
        if (lastState == null || !initialized) return;

        // Access nested fields via 'reported'
        displayedProgress = Mathf.Lerp(displayedProgress, (float)lastState.reported.progressPct, Time.deltaTime * progressLerp);
        displayedNozzle = Mathf.Lerp(displayedNozzle, (float)lastState.reported.nozzleC, Time.deltaTime * tempLerp);
        displayedBed = Mathf.Lerp(displayedBed, (float)lastState.reported.bedC, Time.deltaTime * tempLerp);

        if (progressText != null) progressText.text = progressPrefix + $"{displayedProgress:F1}%";
        if (progressSlider != null) progressSlider.value = Mathf.Clamp01(displayedProgress / 100f);
        if (bedTempText != null) bedTempText.text = bedTempPrefix + $"{displayedBed:F1}°C";
        if (nozzleTempText != null) nozzleTempText.text = nozzlePrefixSafe(displayedNozzle);
    }

    private string nozzlePrefixSafe(double nozzle)
    {
        return nozzleTempPrefix + $"{nozzle:F1}°C";
    }

    /// <summary>
    /// Clear UI to indicate no bound printer or no data available.
    /// </summary>
    private void ClearDisplay()
    {
        lastState = null;
        initialized = false;
        if (statusText != null) statusText.text = statusPrefix + "--";
        if (progressText != null) progressText.text = progressPrefix + "--";
        if (progressSlider != null) progressSlider.value = 0f;
        if (bedTempText != null) bedTempText.text = bedTempPrefix + "--";
        if (nozzleTempText != null) nozzleTempText.text = nozzleTempPrefix + "--";
        if (statusLight != null) statusLight.color = Color.gray;
        
        if (printerIdLabel != null) printerIdLabel.text = "--"; 
    }

    /// <summary>
    /// Display a "loading" style while waiting for first data.
    /// </summary>
    private void ClearDisplayAsLoading()
    {
        lastState = null;
        initialized = false;
        if (statusText != null) statusText.text = statusPrefix + "Loading...";
        if (progressText != null) progressText.text = progressPrefix + "…";
        if (progressSlider != null) progressSlider.value = 0f;
        if (bedTempText != null) bedTempText.text = bedTempPrefix + "--";
        if (nozzleTempText != null) nozzleTempText.text = nozzleTempPrefix + "--";
        if (statusLight != null) statusLight.color = Color.grey;
        
        // Display fallback name or ID while loading (uses simplified logic)
        if (printerIdLabel != null) printerIdLabel.text = GetBestAvailableName(null, CurrentPrinterId, fallbackGameObjectName);
    }

    private void UpdateStatusLight(string status)
    {
        if (statusLight == null) return;

        var color = Color.gray;
        if (!string.IsNullOrEmpty(status))
        {
            switch (status.ToLowerInvariant())
            {
                case "printing":
                case "print":
                case "running":
                    color = Color.green;
                    break;
                case "warming up":
                case "warming":
                case "pausing":
                case "paused":
                    color = Color.yellow;
                    break;
                case "error":
                case "jammed":
                case "fault":
                    color = Color.red;
                    break;
                case "idle":
                case "ready":
                    color = Color.cyan;
                    break;
                default:
                    color = Color.gray;
                    break;
            }
        }
        statusLight.color = color;
    }

    public static string ToTitleCaseSafe(string s)
    {
        if (string.IsNullOrEmpty(s)) return s ?? string.Empty;
        try
        {
            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(s.ToLowerInvariant());
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error in ToTitleCaseSafe: {ex.Message}");
            return s;
        }
    }
}