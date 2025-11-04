using System;
using UnityEngine;

[Serializable]
public class PrinterData
{
    public string PrinterId;
    public string Status;
    public double Progress;
    public double NozzleTemp;
    public double BedTemp;
}