using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Newtonsoft.Json.Linq;
using System.Linq;

public class DashboardManager : MonoBehaviour
{
    public static DashboardManager Instance { get; private set; }

    private readonly Dictionary<string, PrinterState> latestStates = new Dictionary<string, PrinterState>();

    [Tooltip("Assign the single UI panel used to show any printer.")]
    public PrinterUIHandler activePanel;

    private string activePrinterId = string.Empty;
    private static SynchronizationContext unitySyncContext;
    private SseClient _sseClient;
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

        unitySyncContext = SynchronizationContext.Current ?? new SynchronizationContext();

        _sseClient = FindObjectOfType<SseClient>();
        if (_sseClient != null)
            _sseClient.OnDiffPatchReceived += ReceiveDiffPatch;
        
        _pollingClient = FindObjectOfType<PrinterPollingClient>();

        if (activePanel != null) activePanel.SetPrinterId(string.Empty);
    }

    public void ShowPrinter(string printerId, string gameObjectName)
    {
        activePrinterId = printerId ?? string.Empty;
        string fallbackName = gameObjectName ?? string.Empty;

        if (SynchronizationContext.Current == unitySyncContext)
            BindActivePanelAndApplyState(fallbackName);
        else
            unitySyncContext.Post(_ => BindActivePanelAndApplyState(fallbackName), null);

        // --- SSE Connection Logic ---
        if (!string.IsNullOrEmpty(activePrinterId) && _sseClient != null)
        {
            _sseClient.StartSseConnection(activePrinterId);
            
            // Force an immediate poll so the UI isn't stuck on "Loading" 
            // while waiting for the first SSE change.
            if (_pollingClient != null)
            {
                _pollingClient.RequestSingle(activePrinterId);
            }
        }
        else if (string.IsNullOrEmpty(activePrinterId) && _sseClient != null)
        {
            _sseClient.StopSseConnection();
        }
    }

    public void ShowPrinter(string printerId) => ShowPrinter(printerId, null);

    public void ReceiveDiffPatch(string devId, Dictionary<string, JToken> diffPatchFields)
    {
        if (string.IsNullOrEmpty(devId) || diffPatchFields == null) return;

        unitySyncContext.Post(_ =>
        {
            // Use your specific class names here: MetaData() instead of Meta()
            if (!latestStates.TryGetValue(devId, out PrinterState existingState))
            {
                existingState = new PrinterState 
                { 
                    devId = devId, 
                    reported = new ReportedState(), 
                    meta = new MetaData() 
                };
                latestStates[devId] = existingState;
            }

            ApplyDiffPatchToState(existingState, diffPatchFields);
            
            // Update the UI
            if (devId == activePrinterId && activePanel != null)
            {
                activePanel.ApplyPartialUpdate(devId, diffPatchFields);
            }
        }, null);
    }

    private void ApplyDiffPatchToState(PrinterState targetState, Dictionary<string, JToken> diffPatchFields)
    {
        foreach (var entry in diffPatchFields)
        {
            string key = entry.Key;
            JToken value = entry.Value;

            // Handle both "reported.state" and just "state" depending on how backend serializes the Diff
            if (key.Contains("state")) targetState.reported.state = value.ToString();
            else if (key.Contains("progressPct")) targetState.reported.progressPct = value.ToObject<double>();
            else if (key.Contains("nozzleC")) targetState.reported.nozzleC = value.ToObject<double>();
            else if (key.Contains("bedC")) targetState.reported.bedC = value.ToObject<double>();
            else if (key.Contains("name")) targetState.meta.name = value.ToString();
        }
    }

    public void ReceivePrinterStates(PrinterState[] states)
    {
        if (states == null) return;
        foreach (var s in states)
        {
            if (s == null || string.IsNullOrEmpty(s.devId)) continue;
            latestStates[s.devId] = s;

            if (s.devId == activePrinterId && activePanel != null)
            {
                activePanel.DisplayState(s);
            }
        }
    }

    private void BindActivePanelAndApplyState(string fallbackName)
    {
        if (activePanel == null) return;

        if (string.IsNullOrEmpty(activePrinterId))
        {
            activePanel.SetPrinterId(string.Empty, fallbackName, null); 
            return;
        }

        latestStates.TryGetValue(activePrinterId, out var cached);
        activePanel.SetPrinterId(activePrinterId, fallbackName, cached);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (_sseClient != null)
        {
            _sseClient.OnDiffPatchReceived -= ReceiveDiffPatch;
            _sseClient.StopSseConnection();
        }
    }
}