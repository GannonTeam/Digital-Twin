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

    // Cache of latest states by PrinterId
    private readonly Dictionary<string, PrinterState> latestStates = new Dictionary<string, PrinterState>();

    // The active single UI panel (the panel that's shown when user opens the dashboard)
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

        unitySyncContext = SynchronizationContext.Current ?? new SynchronizationContext();

        // If activePanel assigned in inspector, ensure it's initially unbound
        if (activePanel != null) activePanel.SetPrinterId(string.Empty);
    }

    #region Public API

    /// <summary>
    /// Bind the single panel to show this printerId. If a cached state exists it will be displayed immediately.
    /// Call with null/empty to unbind/hide.
    /// </summary>
    public void ShowPrinter(string printerId)
    {
        activePrinterId = printerId ?? string.Empty;

        // Ensure UI updates happen on main thread
        if (SynchronizationContext.Current == unitySyncContext)
        {
            BindActivePanelAndApplyState();
        }
        else
        {
            unitySyncContext.Post(_ => BindActivePanelAndApplyState(), null);
        }

        // Optionally, attempt to fetch initial state from server if cache is absent
        if (!string.IsNullOrEmpty(activePrinterId) && !latestStates.ContainsKey(activePrinterId))
        {
            // If your Polling client supports per-id requests, call it here.
            // Otherwise, request a full poll once so cache populates:
            var poller = FindObjectOfType<PrinterPollingClient>();
            if (poller != null)
            {
                poller.RequestOnce(); // will update cache when response arrives
            }
        }
    }

    /// <summary>
    /// Unbind the active panel (hide or clear).
    /// </summary>
    public void ClearActivePanel()
    {
        ShowPrinter(string.Empty);
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
        if (state == null || string.IsNullOrEmpty(state.PrinterId)) return;

        if (SynchronizationContext.Current == unitySyncContext)
        {
            ReceivePrinterStates(new PrinterState[] { state });
        }
        else
        {
            unitySyncContext.Post(_ => ReceivePrinterStates(new PrinterState[] { state }), null);
        }
    }

    #endregion

    /// <summary>
    /// Must run on main thread.
    /// Updates cache and the active panel if it matches.
    /// </summary>
    public void ReceivePrinterStates(PrinterState[] states)
    {
        if (states == null || states.Length == 0) return;

        foreach (var s in states)
        {
            if (s == null || string.IsNullOrEmpty(s.PrinterId)) continue;

            // update cache
            latestStates[s.PrinterId] = s;

            // if the active panel is showing this printer, update it immediately
            if (!string.IsNullOrEmpty(activePrinterId) && s.PrinterId == activePrinterId && activePanel != null)
            {
                activePanel.DisplayState(s);
            }
        }
    }

    /// <summary>
    /// Helper called on main thread to bind panel and apply cached state (if any).
    /// </summary>
    private void BindActivePanelAndApplyState()
    {
        if (activePanel == null) return;

        if (string.IsNullOrEmpty(activePrinterId))
        {
            // unbind
            activePanel.SetPrinterId(string.Empty);
            return;
        }

        // If we have a cached state for this printer, pass it as initial state
        latestStates.TryGetValue(activePrinterId, out var cached);
        activePanel.SetPrinterId(activePrinterId, cached);
    }

    #region Utilities

    /// <summary>
    /// Read-only access (optional) to the latest cache for external UI or debugging.
    /// </summary>
    public bool TryGetLatestState(string printerId, out PrinterState state)
    {
        return latestStates.TryGetValue(printerId, out state);
    }

    #endregion

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}