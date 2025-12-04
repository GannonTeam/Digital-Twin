using System;
using UnityEngine;

// --- Data Model: PrinterState ---
[Serializable]
public class PrinterState
{
    public string devId = string.Empty;

    // ADDED: Nested class to capture the 'meta' object
    public MetaData meta = new MetaData(); 

    public ReportedState reported = new ReportedState();
}

// --- Nested Class: MetaData (for name) ---
[Serializable]
public class MetaData
{
    // Captures the friendly name from the JSON: "meta": {"name": "A1-P1S"}
    public string name = string.Empty;
    
    // Note: The devId is also present here but we ignore it since it exists at the root.
}

// --- Nested Class: ReportedState (for telemetry) ---
[Serializable]
public class ReportedState
{
    public string state = "unknown"; 
    public double progressPct = 0.0;
    public double nozzleC = 0.0;
    public double bedC = 0.0;
}