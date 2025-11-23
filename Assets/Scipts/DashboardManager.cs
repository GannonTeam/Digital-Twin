using UnityEngine;
using System.Collections.Generic;

// Attach this script to a single GameObject, like your Canvas or a Scene Manager.
public class DashboardManager : MonoBehaviour
{
    [Tooltip("Drag the GameObject with the SignalRConnector script here.")]
    public SignalRConnector connector; 

    // Dictionary to hold all 40 UI Handlers, keyed by their unique PrinterId for fast lookup.
    private Dictionary<string, PrinterUIHandler> printerPanelUIs = new Dictionary<string, PrinterUIHandler>();

    void Awake()
    {
        // Safety check to automatically find the connector if it wasn't assigned in the Inspector.
        if (connector == null)
        {
            connector = FindObjectOfType<SignalRConnector>();
        }
        
        if (connector == null)
        {
            Debug.LogError("DashboardManager requires a SignalRConnector to be present in the scene.");
            enabled = false; // Disable script if dependency is missing
            return;
        }

        // 1. Initialize the UI handlers dictionary. 
        // We find all PrinterUIHandler components that are children of this GameObject.
        MapPrinterUIs();

        // 2. Subscribe to the event *before* Start() so we don't miss any data
        connector.OnPrinterDataReceived += HandleIncomingPrinterData;
    }

    void Start()
    {
        // ConnectAsync is called in SignalRConnector.Start(), and RequestInitialState() 
        // is called automatically after a successful connection.
    }

    /// <summary>
    /// Finds all PrinterUIHandler components in the scene's children and maps them
    /// to the dictionary using their assigned PrinterId.
    /// </summary>
    private void MapPrinterUIs()
    {
        // Get all handlers attached to your individual panels (children of this manager)
        PrinterUIHandler[] allPanels = GetComponentsInChildren<PrinterUIHandler>(true);
        int mappedCount = 0;
        
        foreach(var panel in allPanels)
        {
            if (!string.IsNullOrEmpty(panel.PrinterId))
            {
                if (!printerPanelUIs.ContainsKey(panel.PrinterId))
                {
                    printerPanelUIs.Add(panel.PrinterId, panel);
                    mappedCount++;
                }
                else
                {
                    Debug.LogError($"Duplicate Printer ID found: {panel.PrinterId}. Check your UI panels.");
                }
            }
        }
        Debug.Log($"Dashboard Manager initialized. Mapped {mappedCount} printer UI panels.");
    }

    /// <summary>
    /// Handler for the SignalRConnector's event. This runs safely on the main thread.
    /// </summary>
    /// <param name="data">The latest PrinterData.</param>
    private void HandleIncomingPrinterData(PrinterData data)
    {
        // 1. Look up the correct UI panel using the PrinterId
        if (printerPanelUIs.TryGetValue(data.PrinterId, out PrinterUIHandler uiComponent))
        {
            // 2. Pass the data to the UI handler for visual updates
            uiComponent.DisplayData(data);
        }
        else
        {
            // This is expected if an initial data burst comes in before all UIs are mapped,
            // or if the backend sends data for an unknown printer.
            Debug.LogWarning($"Data received for unmapped Printer ID: {data.PrinterId}.");
        }
    }

    void OnDestroy()
    {
        // ALWAYS unsubscribe to prevent memory leaks!
        if (connector != null)
        {
            connector.OnPrinterDataReceived -= HandleIncomingPrinterData;
        }
    }
}
