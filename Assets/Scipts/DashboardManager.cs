using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Newtonsoft.Json.Linq; // Added for JToken support
using System.Linq; // Added for LINQ functionalities

/// <summary>
/// DashboardManager that caches latest PrinterState per printer,
/// supports binding a single PrinterUIHandler panel dynamically via ShowPrinter(printerId),
/// and updates the active panel when new states arrive.
/// Thread-safe: ReceivePrinterJson/ReceivePrinterState can be called from any thread.
/// </summary>
public class DashboardManager : MonoBehaviour
{
    public static DashboardManager Instance { get; private set; }

    // Cache of latest states by devId (the new identifier from JSON)
    private readonly Dictionary<string, PrinterState> latestStates = new Dictionary<string, PrinterState>();

    [Tooltip("Assign the single UI panel used to show any printer. Can be left empty and assigned at runtime.")]
    public PrinterUIHandler activePanel;

    // Which printerId is currently shown in the activePanel (empty if none)
    private string activePrinterId = string.Empty;

    // Captured Unity synchronization context
    private static SynchronizationContext unitySyncContext;

    // Reference to the SseClient for real-time updates
    private SseClient _sseClient;

    // Reference to the PrinterPollingClient for background data fetching
    private PrinterPollingClient _pollingClient;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(this.gameObject);

        // Capture the main thread's synchronization context
        unitySyncContext = SynchronizationContext.Current ?? new SynchronizationContext();

        // Get SseClient reference
        _sseClient = FindObjectOfType<SseClient>();
        if (_sseClient == null)
        {
            Debug.LogWarning("DashboardManager: SseClient not found in scene. Real-time updates will not function.");
        }
        else
        {
            // Subscribe to SSE events
            _sseClient.OnDiffPatchReceived += ReceiveDiffPatch;
        }
        
        // Get PrinterPollingClient reference
        _pollingClient = FindObjectOfType<PrinterPollingClient>();
        if (_pollingClient == null)
        {
            Debug.LogWarning("DashboardManager: PrinterPollingClient not found in scene. Polling functions will not work.");
        }

        // If activePanel assigned in inspector, ensure it's initially unbound.
        // Calls the original SetPrinterId(string) signature, which defaults the fallback name to null.
        if (activePanel != null) activePanel.SetPrinterId(string.Empty);
    }

    // --- Public API ---

    /// <summary>
    /// Bind the single panel to show this printerId. If a cached state exists it will be displayed immediately.
    /// Call with null/empty to unbind/hide.
    /// </summary>
    // MODIFIED: Added gameObjectName parameter to accept the fallback name from the click handler.
    public void ShowPrinter(string printerId, string gameObjectName)
    {
        activePrinterId = printerId ?? string.Empty;
        string fallbackName = gameObjectName ?? string.Empty; // Store the fallback name locally for the binding call

        // Ensure UI updates happen on main thread
        if (SynchronizationContext.Current == unitySyncContext)
        {
            BindActivePanelAndApplyState(fallbackName);
        }
        else
        {
            // Pass the fallback name via the state object to BindActivePanelAndApplyState
            unitySyncContext.Post(_ => BindActivePanelAndApplyState(fallbackName), null);
        }

        // --- SSE Connection Logic ---
        if (!string.IsNullOrEmpty(activePrinterId) && _sseClient != null)
        {
            if (_sseClient.CurrentDevId != activePrinterId)
            {
                _sseClient.StartSseConnection(activePrinterId);
            }
        }
        else if (string.IsNullOrEmpty(activePrinterId) && _sseClient != null)
        {
            _sseClient.StopSseConnection();
        }
        // --- End SSE Connection Logic ---
    }
    
    /// <summary>
    /// OVERLOAD: Maintains compatibility for internal calls that don't have the GameObject name.
    /// </summary>
    public void ShowPrinter(string printerId)
    {
        // When called without a gameObjectName, pass a null string to the main handler.
        ShowPrinter(printerId, null);
    }


    /// <summary>
    /// Unbind the active panel (hide or clear).
    /// </summary>
    public void ClearActivePanel()
    {
        // Calls the new ShowPrinter(string, string) overload
        ShowPrinter(string.Empty, null); 
    }

    /// <summary>
    /// Thread-safe: call from any thread with JSON payload (array or single object).
    /// </summary>
    public void ReceivePrinterJson(string json)
    {
        if (string.IsNullOrEmpty(json)) return;

        // Post parse+deliver to main thread
        unitySyncContext.Post(_ =>
        {
            try
            {
                // JsonHelper handles both single object and array JSON.
                var arr = JsonHelper.FromJson<PrinterState>(json);
                ReceivePrinterStates(arr);
            }
            catch (System.Exception ex)
            {
                Debug.LogError("DashboardManager JSON parse error: " + ex + "\nRaw: " + json);
            }
        }, null);
    }

    /// <summary>
    /// Thread-safe delivery of a single state.
    /// </summary>
    public void ReceivePrinterState(PrinterState state)
    {
        // Check for the new ID field 'devId'
        if (state == null || string.IsNullOrEmpty(state.devId)) return;

        if (SynchronizationContext.Current == unitySyncContext)
        {
            ReceivePrinterStates(new PrinterState[] { state });
        }
        else
        {
            unitySyncContext.Post(_ => ReceivePrinterStates(new PrinterState[] { state }), null);
        }
    }
    
    /// <summary>
    /// Thread-safe: Applies a DiffPatch to an existing PrinterState.
    /// </summary>
    /// <param name="devId">The ID of the printer to update.</param>
    /// <param name="diffPatchFields">A dictionary of fields with dot-notation keys (e.g., "reported.state") and their new values as JTokens.</param>
    public void ReceiveDiffPatch(string devId, Dictionary<string, JToken> diffPatchFields)
    {
        if (string.IsNullOrEmpty(devId) || diffPatchFields == null || diffPatchFields.Count == 0) return;

        unitySyncContext.Post(_ =>
        {
            if (latestStates.TryGetValue(devId, out PrinterState existingState))
            {
                // Apply the patch to the existing state
                ApplyDiffPatchToState(existingState, diffPatchFields);
                
                // If this is the active printer, update the UI
                if (!string.IsNullOrEmpty(activePrinterId) && devId == activePrinterId && activePanel != null)
                {
                    activePanel.DisplayState(existingState);
                }
            }
            else
            {
                Debug.LogWarning($"DashboardManager: Received DiffPatch for unknown printer {devId}. Ignoring.");
                // OPTIONAL: If a DiffPatch arrives for an unknown printer, one might trigger a full fetch for it.
                // var poller = FindObjectOfType<PrinterPollingClient>();
                // poller?.RequestSingle(devId);
            }
        }, null);
    }

    /// <summary>
    /// Applies the diff patch fields to the target PrinterState object.
    /// Handles dot-notation keys for nested properties.
    /// </summary>
    private void ApplyDiffPatchToState(PrinterState targetState, Dictionary<string, JToken> diffPatchFields)
    {
        foreach (var entry in diffPatchFields)
        {
            string key = entry.Key;
            JToken value = entry.Value;

            if (key.StartsWith("reported."))
            {
                string reportedField = key.Substring("reported.".Length);
                switch (reportedField)
                {
                    case "state":
                        targetState.reported.state = value.ToObject<string>();
                        break;
                    case "progress_pct":
                        targetState.reported.progressPct = value.ToObject<double>();
                        break;
                    case "nozzle_c":
                        targetState.reported.nozzleC = value.ToObject<double>();
                        break;
                    case "bed_c":
                        targetState.reported.bedC = value.ToObject<double>();
                        break;
                    // Add more reported fields here as needed
                    default:
                        Debug.LogWarning($"DashboardManager: Unhandled reported field in DiffPatch: {key}");
                        break;
                }
            }
            else if (key.StartsWith("meta."))
            {
                string metaField = key.Substring("meta.".Length);
                switch (metaField)
                {
                    case "name":
                        targetState.meta.name = value.ToObject<string>();
                        break;
                    // Add more meta fields here as needed
                    default:
                        Debug.LogWarning($"DashboardManager: Unhandled meta field in DiffPatch: {key}");
                        break;
                }
            }
            else if (key == "devId")
            {
                // devId should ideally not change in a diff patch, but if it does, handle it
                targetState.devId = value.ToObject<string>();
            }
            else
            {
                Debug.LogWarning($"DashboardManager: Unhandled top-level field in DiffPatch: {key}");
            }
        }
    }

    // --- Internal Methods (Must run on main thread) ---

    /// <summary>
    /// Must run on main thread.
    /// Updates cache and the active panel if it matches.
    /// </summary>
    public void ReceivePrinterStates(PrinterState[] states)
    {
        if (states == null || states.Length == 0) return;

        foreach (var s in states)
        {
            // Use 's.devId' for identification
            if (s == null || string.IsNullOrEmpty(s.devId)) continue;

            // update cache: key is now 's.devId'
            latestStates[s.devId] = s;

            // if the active panel is showing this printer, update it immediately
            // Compare 's.devId' with the active ID
            if (!string.IsNullOrEmpty(activePrinterId) && s.devId == activePrinterId && activePanel != null)
            {
                activePanel.DisplayState(s);
            }
        }
    }

    /// <summary>
    /// Helper called on main thread to bind panel and apply cached state (if any).
    /// </summary>
    // MODIFIED: Accepts the fallback name from ShowPrinter
    private void BindActivePanelAndApplyState(string fallbackName)
    {
        if (activePanel == null) return;

        if (string.IsNullOrEmpty(activePrinterId))
        {
            // FIX: When clearing, call the SetPrinterId(ID, Name, State) signature
            // to ensure the name fallback is still applied for the clear state.
            activePanel.SetPrinterId(string.Empty, fallbackName, null); 
            return;
        }

        // If we have a cached state for this printer, pass it as initial state
        latestStates.TryGetValue(activePrinterId, out var cached);
    
        // Calls the new SetPrinterId(string, string, PrinterState) overload
        activePanel.SetPrinterId(activePrinterId, fallbackName, cached);
    }
    
    // OVERLOAD for internal calls that don't need to pass the name (e.g., from Post)
    private void BindActivePanelAndApplyState()
    {
        BindActivePanelAndApplyState(null);
    }


    // --- Utilities ---

    /// <summary>
    /// Read-only access (optional) to the latest cache for external UI or debugging.
    /// </summary>
    public bool TryGetLatestState(string printerId, out PrinterState state)
    {
        return latestStates.TryGetValue(printerId, out state);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;

        if (_sseClient != null)
        {
            _sseClient.OnDiffPatchReceived -= ReceiveDiffPatch;
            _sseClient.StopSseConnection(); // Ensure SSE connection is closed when DashboardManager is destroyed
        }
    }
}