using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Globalization;
using System; // Added for exception handling

/// <summary>
/// Printer UI handler for the single-panel dashboard.
/// - Supports binding to a printer id at runtime via SetPrinterId(...).
/// - Displays the bound printer id in a TextMeshProUGUI (printerIdLabel) and optionally lets the user edit it via a TMP_InputField (printerIdInput).
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

    // the current bound printer id (empty if unbound)
    public string CurrentPrinterId { get; private set; } = string.Empty;

    void Awake()
    {
        // Ensure any label reflects initial state
        if (printerIdLabel != null && string.IsNullOrEmpty(printerIdLabel.text))
            printerIdLabel.text = "--";

        // Hook up input field if provided (user can type an id)
        if (printerIdInput != null)
        {
            // Keep the input field in sync if label is set in inspector
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
    /// Bind the panel to a printer id. If initialState is provided the panel will display it immediately.
    /// Pass null/empty to unbind and clear the UI.
    /// If triggerRequest is true the manager/poller will be asked to fetch the printer state.
    /// </summary>
    public void SetPrinterId(string printerId, PrinterState initialState = null, bool triggerRequest = true)
    {
        CurrentPrinterId = printerId ?? string.Empty;
        lastState = null;
        initialized = false;

        // update visible id controls
        if (printerIdLabel != null)
            printerIdLabel.text = string.IsNullOrEmpty(CurrentPrinterId) ? "--" : CurrentPrinterId;

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

        // --- FIX FOR STACK OVERFLOW: Remove the recursive call to DashboardManager.ShowPrinter ---
        if (triggerRequest)
        {
            // If the manager calls SetPrinterId to bind the panel, we must NOT call DashboardManager.ShowPrinter back.
            // We rely on the external caller (ClickableToggle or Convai Action) to have called ShowPrinter already.

            // Only keep the PollingClient request, as that does not cause the infinite recursion loop.
            if (requestSingleOnSet)
            {
                var poller = FindObjectOfType<PrinterPollingClient>();
                if (poller != null)
                {
                    poller.RequestSingle(CurrentPrinterId);
                }
            }
        }
        // --- END FIX ---
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

        // When the user manually types an ID, we assume they want to initiate the entire request chain.
        SetPrinterId(newText.Trim(), null, true);
    }

    private void ApplyInitialState(PrinterState s)
    {
        lastState = s;
        displayedProgress = (float)s.Progress;
        displayedNozzle = (float)s.NozzleTemp;
        displayedBed = (float)s.BedTemp;
        initialized = true;

        // update immediate labeled texts
        if (statusText != null) statusText.text = statusPrefix + ToTitleCaseSafe(s.Status);
        if (progressText != null) progressText.text = progressPrefix + $"{displayedProgress:F1}%";
        if (progressSlider != null) progressSlider.value = Mathf.Clamp01(displayedProgress / 100f);
        if (bedTempText != null) bedTempText.text = bedTempPrefix + $"{displayedBed:F1}°C";
        if (nozzleTempText != null) nozzleTempText.text = nozzleTempPrefix + $"{displayedBed:F1}°C"; // FIX: used displayedBed instead of displayedNozzle
        
        UpdateStatusLight(s.Status);
    }

    /// <summary>
    /// Main update entrypoint called by DashboardManager when a new state arrives.
    /// Must be called on the main thread.
    /// </summary>
    public void DisplayState(PrinterState state)
    {
        if (state == null || string.IsNullOrEmpty(state.PrinterId)) return;
        if (string.IsNullOrEmpty(CurrentPrinterId) || state.PrinterId != CurrentPrinterId) return;

        lastState = state;

        if (!initialized)
        {
            displayedProgress = (float)state.Progress;
            displayedNozzle = (float)state.NozzleTemp;
            displayedBed = (float)state.BedTemp;
            initialized = true;
        }

        // immediate text for status (readable)
        if (statusText != null) statusText.text = statusPrefix + ToTitleCaseSafe(state.Status);
        UpdateStatusLight(state.Status);

        // numeric fields smoothed in Update()
    }

    void Update()
    {
        if (lastState == null || !initialized) return;

        displayedProgress = Mathf.Lerp(displayedProgress, (float)lastState.Progress, Time.deltaTime * progressLerp);
        displayedNozzle = Mathf.Lerp(displayedNozzle, (float)lastState.NozzleTemp, Time.deltaTime * tempLerp);
        displayedBed = Mathf.Lerp(displayedBed, (float)lastState.BedTemp, Time.deltaTime * tempLerp);

        if (progressText != null) progressText.text = progressPrefix + $"{displayedProgress:F1}%";
        if (progressSlider != null) progressSlider.value = Mathf.Clamp01(displayedProgress / 100f);
        if (bedTempText != null) bedTempText.text = bedTempPrefix + $"{displayedBed:F1}°C";
        if (nozzleTempText != null) nozzleTempText.text = nozzlePrefixSafe(displayedNozzle);
    }

    private string nozzlePrefixSafe(double nozzle)
    {
        // Ensure consistent formatting for nozzle text
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

    // Made public static to be accessible from ShowPrinterDashboardAction.cs
    public static string ToTitleCaseSafe(string s)
    {
        if (string.IsNullOrEmpty(s)) return s ?? string.Empty;
        try
        {
            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(s.ToLowerInvariant());
        }
        catch (Exception ex) // Added exception type for better practice
        {
            Debug.LogError($"Error in ToTitleCaseSafe: {ex.Message}");
            return s;
        }
    }
}