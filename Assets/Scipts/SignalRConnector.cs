using UnityEngine;
using Microsoft.AspNetCore.SignalR.Client;
using System.Threading.Tasks;

public class SignalRConnector : MonoBehaviour
{
    private HubConnection connection;
    private readonly string hubUrl = "http://localhost:5000/myHub"; // <--- CHANGE THIS URL

    void Start()
    {
        // 1. Build the connection
        connection = new HubConnectionBuilder()
            .WithUrl(hubUrl)
            .Build();

        // 2. Set up event listeners (before starting the connection)
        connection.On<string>("ReceiveMessage", (message) =>
        {
            // The "ReceiveMessage" method must match a method name on your backend Hub
            Debug.Log($"SignalR Message Received: {message}");
            // Use UnityMainThreadDispatcher if you need to update UI/GameObjects
            // as SignalR callbacks run on background threads.
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
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error connecting to SignalR: {ex.Message}");
        }
    }

    // Example method to send data to the backend
    public async void SendData(string user, string data)
    {
        if (connection.State == HubConnectionState.Connected)
        {
            try
            {
                // "SendMessage" is the method name on your backend Hub
                await connection.InvokeAsync("SendMessage", user, data);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error invoking SendMessage: {ex.Message}");
            }
        }
    }

    // Ensure connection is stopped when the game object is destroyed
    void OnDestroy()
    {
        _ = connection?.StopAsync();
    }
}