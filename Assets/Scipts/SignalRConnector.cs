using UnityEngine;
using Microsoft.AspNetCore.SignalR.Client;
using System.Threading.Tasks;
using Convai.Scripts.Runtime.Utils; // <-- USING YOUR CUSTOM DISPATCHER
using System;

// Wrapper class needed for Unity's JsonUtility to deserialize a JSON array of objects.
// The backend must wrap the array in an object like: {"Printers": [ {data}, {data}, ... ]}
[Serializable]
public class PrinterDataArrayWrapper
{
    public PrinterData[] Printers;
}

public class SignalRConnector : MonoBehaviour
{
    private HubConnection connection;
    
    // NOTE: Updated the hub name to a more descriptive 'printerHub'
    private readonly string hubUrl = "https://digitwinbackend.quangphuly.online/printerHub"; 
    
    // Event the DashboardManager will subscribe to for receiving updates.
    public event Action<PrinterData> OnPrinterDataReceived;

    void Start()
    {
        // 1. Build the connection
        connection = new HubConnectionBuilder()
            .WithUrl(hubUrl)
            .WithAutomaticReconnect() // Recommended for reliable connection
            .Build();

        // 2. Set up the REAL-TIME PUSH LISTENER
        // "ReceivePrinterUpdate" is the method name your backend MUST call on clients.
        connection.On<string>("ReceivePrinterUpdate", (jsonPayload) =>
        {
            // CRITICAL: Dispatch the deserialization and event firing to the Main Thread.
            MainThreadDispatcher.Instance.RunOnMainThread(() =>
            {
                try
                {
                    PrinterData data = JsonUtility.FromJson<PrinterData>(jsonPayload);
                    OnPrinterDataReceived?.Invoke(data);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"SignalR: Failed to deserialize printer data: {ex.Message}");
                }
            });
        });

        // 3. Start the connection
        _ = ConnectAsync();
    }

    private async Task ConnectAsync()
    {
        try
        {
            await connection.StartAsync();
            Debug.Log("SignalR Connection Started successfully.");
            
            // OPTIONAL: Immediately request the initial state after connecting
            _ = RequestInitialState();
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error connecting to SignalR: {ex.Message}");
        }
    }
    
    // Method for the client to request the full state of all 40 printers (Initial Pull)
    public async Task RequestInitialState()
    {
        if (connection.State == HubConnectionState.Connected)
        {
            try
            {
                // Backend Hub method that returns a JSON string of all 40 printers.
                string initialStateJson = await connection.InvokeAsync<string>("GetInitialDashboardState");
                
                // Dispatch array deserialization and event firing to the Main Thread.
                MainThreadDispatcher.Instance.RunOnMainThread(() =>
                {
                    PrinterDataArrayWrapper wrapper = JsonUtility.FromJson<PrinterDataArrayWrapper>(initialStateJson);
                    
                    if (wrapper?.Printers != null)
                    {
                        // Fire the same event for each printer in the initial list
                        foreach (var printer in wrapper.Printers)
                        {
                            OnPrinterDataReceived?.Invoke(printer); 
                        }
                        Debug.Log($"Successfully loaded initial state for {wrapper.Printers.Length} printers.");
                    }
                    else
                    {
                        Debug.LogError("Initial state JSON was empty or incorrectly formatted.");
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error requesting initial state: {ex.Message}");
            }
        }
    }

    // Example method to send a command back to the backend
    public async void SendData(string user, string data)
    {
        if (connection.State == HubConnectionState.Connected)
        {
            try
            {
                // "SendCommand" should be the method name on your backend Hub
                await connection.InvokeAsync("SendCommand", user, data);
                Debug.Log($"Sent command from {user}: {data}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error invoking SendCommand: {ex.Message}");
            }
        }
    }

    // Ensure connection is stopped when the game object is destroyed
    void OnDestroy()
    {
        _ = connection?.StopAsync();
    }
}
