using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Globalization;
using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

/// <summary>
/// Printer UI handler for the single-panel dashboard.
/// - Supports full state updates (Polling) and partial updates (SSE).
/// - Smooths numeric values and handles status color logic.
/// </summary>
public class PrinterUIHandler : MonoBehaviour
{
    [Header("UI References (Assign Text Components)")]
    [Tooltip("Label showing the currently bound Printer Id or GameObjectName.")]
    public TextMeshProUGUI printerIdLabel;

    [Tooltip("Optional editable input for entering a PrinterId.")]
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

    // Internal State
    private PrinterState lastState;
    private float displayedProgress;
    private float displayedNozzle;
    private float displayedBed;
    private bool initialized;
    private string fallbackGameObjectName = string.Empty;

    public string CurrentPrinterId { get; private set; } = string.Empty;

    void Awake()
    {
        if (printerIdLabel != null && string.IsNullOrEmpty(printerIdLabel.text))
            printerIdLabel.text = "--";

        if (printerIdInput != null)
        {
            printerIdInput.onEndEdit.AddListener(OnPrinterIdInputEndEdit);
        }
    }

    void OnDestroy()
    {
        if (printerIdInput != null)
            printerIdInput.onEndEdit.RemoveListener(OnPrinterIdInputEndEdit);
    }

    /// <summary>
    /// Binds the panel to a printer id. 
    /// </summary>
    public void SetPrinterId(string printerId, string gameObjectName, PrinterState initialState = null, bool triggerRequest = true)
    {
        CurrentPrinterId = printerId ?? string.Empty;
        fallbackGameObjectName = gameObjectName ?? string.Empty;
        lastState = null;
        initialized = false;

        string initialLabel = GetBestAvailableName(initialState, CurrentPrinterId, fallbackGameObjectName);
        if (printerIdLabel != null) printerIdLabel.text = initialLabel;

        if (printerIdInput != null && printerIdInput.text != CurrentPrinterId)
            printerIdInput.text = CurrentPrinterId;

        if (string.IsNullOrEmpty(CurrentPrinterId))
        {
            ClearDisplay();
            return;
        }

        if (initialState != null) ApplyInitialState(initialState);
        else ClearDisplayAsLoading();
    }

    /// <summary>
    /// Overload for simpler calls.
    /// </summary>
    public void SetPrinterId(string printerId, PrinterState initialState = null, bool triggerRequest = true)
    {
        SetPrinterId(printerId, null, initialState, triggerRequest);
    }

    /// <summary>
    /// Updates the UI using a partial dictionary (from SSE).
    /// </summary>
    public void ApplyPartialUpdate(string devId, Dictionary<string, JToken> updates)
    {
        if (devId != CurrentPrinterId || updates == null) return;

        // If we don't even have a state object yet, create a dummy one to hold data
        if (lastState == null)
        {
            lastState = new PrinterState { devId = devId, reported = new ReportedState() };
        }

        // Update the internal values. The Update() loop will handle the Lerp.
        if (updates.TryGetValue("progressPct", out JToken p)) lastState.reported.progressPct = p.Value<double>();
        if (updates.TryGetValue("nozzleC", out JToken n)) lastState.reported.nozzleC = n.Value<double>();
        if (updates.TryGetValue("bedC", out JToken b)) lastState.reported.bedC = b.Value<double>();
        
        if (updates.TryGetValue("state", out JToken s))
        {
            string stateStr = s.Value<string>();
            lastState.reported.state = stateStr;
            
            // Immediate text update for discrete strings
            if (statusText != null) statusText.text = statusPrefix + ToTitleCaseSafe(stateStr);
            UpdateStatusLight(stateStr);
        }

        initialized = true; 
    }

    /// <summary>
    /// Full state update (usually from the Polling fallback).
    /// </summary>
    public void DisplayState(PrinterState state)
    {
        if (state == null || string.IsNullOrEmpty(state.devId)) return;
        if (state.devId != CurrentPrinterId) return;

        lastState = state;

        if (!initialized)
        {
            displayedProgress = (float)state.reported.progressPct;
            displayedNozzle = (float)state.reported.nozzleC;
            displayedBed = (float)state.reported.bedC;
            initialized = true;
        }

        if (printerIdLabel != null)
            printerIdLabel.text = GetBestAvailableName(state, CurrentPrinterId, fallbackGameObjectName);

        if (statusText != null) 
            statusText.text = statusPrefix + ToTitleCaseSafe(state.reported.state);
        
        UpdateStatusLight(state.reported.state);
    }

    void Update()
    {
        if (lastState == null || !initialized) return;

        // Smooth Lerping for numeric values
        displayedProgress = Mathf.Lerp(displayedProgress, (float)lastState.reported.progressPct, Time.deltaTime * progressLerp);
        displayedNozzle = Mathf.Lerp(displayedNozzle, (float)lastState.reported.nozzleC, Time.deltaTime * tempLerp);
        displayedBed = Mathf.Lerp(displayedBed, (float)lastState.reported.bedC, Time.deltaTime * tempLerp);

        // UI Assignments
        if (progressText != null) progressText.text = progressPrefix + $"{displayedProgress:F1}%";
        if (progressSlider != null) progressSlider.value = Mathf.Clamp01(displayedProgress / 100f);
        if (bedTempText != null) bedTempText.text = bedTempPrefix + $"{displayedBed:F1}°C";
        if (nozzleTempText != null) nozzleTempText.text = nozzleTempPrefix + $"{displayedNozzle:F1}°C";
    }

    private string GetBestAvailableName(PrinterState state, string currentId, string fallbackName)
    {
        if (!string.IsNullOrEmpty(fallbackName)) return fallbackName;
        if (state?.meta != null && !string.IsNullOrEmpty(state.meta.name)) return state.meta.name;
        return !string.IsNullOrEmpty(currentId) ? currentId : "--";
    }

    private void OnPrinterIdInputEndEdit(string newText)
    {
        if (string.IsNullOrWhiteSpace(newText)) SetPrinterId(string.Empty, null, false);
        else SetPrinterId(newText.Trim(), null, true);
    }

    private void ApplyInitialState(PrinterState s)
    {
        lastState = s;
        displayedProgress = (float)s.reported.progressPct;
        displayedNozzle = (float)s.reported.nozzleC;
        displayedBed = (float)s.reported.bedC;
        initialized = true;

        if (printerIdLabel != null) 
            printerIdLabel.text = GetBestAvailableName(s, CurrentPrinterId, fallbackGameObjectName);

        if (statusText != null) statusText.text = statusPrefix + ToTitleCaseSafe(s.reported.state);
        UpdateStatusLight(s.reported.state);
    }

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

    private void ClearDisplayAsLoading()
    {
        lastState = null;
        initialized = false;
        if (statusText != null) statusText.text = statusPrefix + "Loading...";
        if (statusLight != null) statusLight.color = Color.gray;
        if (printerIdLabel != null) 
            printerIdLabel.text = GetBestAvailableName(null, CurrentPrinterId, fallbackGameObjectName);
    }

    private void UpdateStatusLight(string status)
    {
        if (statusLight == null) return;
        var color = Color.gray;
        if (!string.IsNullOrEmpty(status))
        {
            switch (status.ToLowerInvariant())
            {
                case "printing": case "running": color = Color.green; break;
                case "warming": case "paused": color = Color.yellow; break;
                case "error": case "fault": color = Color.red; break;
                case "idle": case "ready": color = Color.cyan; break;
            }
        }
        statusLight.color = color;
    }

    public static string ToTitleCaseSafe(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(s.ToLowerInvariant());
    }
}