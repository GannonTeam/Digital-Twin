using System.Collections.Generic;
using System.Threading;
using UnityEngine;

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

        // Optionally, attempt to fetch initial state from server if cache is absent
        if (!string.IsNullOrEmpty(activePrinterId) && !latestStates.ContainsKey(activePrinterId))
        {
            var poller = FindObjectOfType<PrinterPollingClient>();
            if (poller != null)
            {
                // OPTIMIZATION: Request the specific printer's data to ensure we get the latest detailed JSON.
                poller.RequestSingle(activePrinterId); 
            }
        }
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
    }
}