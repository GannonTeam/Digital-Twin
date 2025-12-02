using System;

[Serializable]
public class PrinterState
{
    public string PrinterId = string.Empty;
    public string Status = "unknown";
    public double Progress = 0.0;
    public double NozzleTemp = 0.0;
    public double BedTemp = 0.0;
}