using Convai.Scripts.Runtime.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq; // For JToken

public class SseClient : MonoBehaviour
{
    private const string BackendBaseUrl = "https://digitwinbackend.quangphuly.online";
    private const string SseEndpointFormat = "/twin/stream/printer/{0}"; // {0} for devId

    [Header("Auth")]
    [Tooltip("Bearer token for Authorization header.")]
    public string bearerToken = "";

    private HttpClient _httpClient;
    private CancellationTokenSource _cts;
    private string _currentDevId = string.Empty;

    // Event to notify subscribers about received DiffPatch data
    // The dictionary will contain the parsed fields as JToken values
    public event Action<string, Dictionary<string, JToken>> OnDiffPatchReceived;

    void Awake()
    {
        // HttpClient should ideally be a singleton or managed globally
        // For simplicity in this example, we create one per SseClient
        _httpClient = new HttpClient();
        // Set a reasonable timeout for the initial connection, but not for the stream itself
        _httpClient.Timeout = TimeSpan.FromSeconds(30); 
    }

    void OnDestroy()
    {
        StopSseConnection();
        _httpClient.Dispose();
    }

    /// <summary>
    /// Starts an SSE connection for a given device ID.
    /// </summary>
    public void StartSseConnection(string devId)
    {
        Debug.Log($"SseClient: Attempting to subscribe to SSE for devId: {devId}");
        if (_cts != null && !_cts.IsCancellationRequested && _currentDevId == devId)
        {
            Debug.LogWarning($"SseClient: Already connected to SSE stream for {devId}.");
            return;
        }

        // If connected to a different printer, stop it first
        if (_cts != null && !_cts.IsCancellationRequested && _currentDevId != devId)
        {
            Debug.Log($"SseClient: Stopping existing SSE connection for {_currentDevId} to connect to {devId}.");
            StopSseConnection();
        }

        if (string.IsNullOrEmpty(devId))
        {
            Debug.LogError("SseClient: Cannot start SSE connection, devId is null or empty.");
            return;
        }

        _cts = new CancellationTokenSource();
        _currentDevId = devId;
        string url = BackendBaseUrl + string.Format(SseEndpointFormat, devId);
        Debug.Log($"SseClient: Attempting to connect to SSE for {devId} at {url}");
        _ = ConnectToSseAsync(url, devId, _cts.Token); // _ = to suppress warning about async method not awaited
    }

    /// <summary>
    /// Stops the active SSE connection.
    /// </summary>
    public void StopSseConnection()
    {
        Debug.Log("SseClient: Attempting to unsubscribe from SSE.");
        if (_cts != null)
        {
            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
            _currentDevId = string.Empty;
            Debug.Log("SseClient: SSE connection stopped.");
        }
    }

    private async Task ConnectToSseAsync(string url, string devId, CancellationToken cancellationToken)
    {
        try
        {
            using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url))
            {
                request.Headers.Accept.Clear();
                request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/event-stream"));

                if (!string.IsNullOrEmpty(bearerToken))
                {
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearerToken);
                }

                using (HttpResponseMessage response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
                {
                    response.EnsureSuccessStatusCode(); // Throws if not a 2xx code

                    // Use Stream.ReadAsync for better SSE parsing
                    using (StreamReader reader = new StreamReader(await response.Content.ReadAsStreamAsync()))
                    {
                        Debug.Log($"SseClient: Successfully connected to SSE for {devId}.");
                        while (!cancellationToken.IsCancellationRequested)
                        {
                            string line = await reader.ReadLineAsync();

                            // End of stream can happen on graceful server close or network issue
                            if (line == null)
                            {
                                Debug.LogWarning($"SseClient: SSE stream for {devId} ended unexpectedly (server closed or network issue).");
                                break;
                            }

                            // SSE protocol: "data: " prefix
                            if (line.StartsWith("data:"))
                            {
                                string jsonData = line.Substring("data:".Length).Trim();
                                // Debug.Log($"SseClient: Received data for {devId}: {jsonData}");
                                ProcessSseData(devId, jsonData);
                            }
                            // Heartbeat or comments
                            else if (line.Trim() == ":" || line.StartsWith(":"))
                            {
                                // Debug.Log($"SseClient: Received heartbeat or comment for {devId}: {line}");
                            }
                            else if (!string.IsNullOrEmpty(line))
                            {
                                Debug.LogWarning($"SseClient: Received unexpected SSE line for {devId}: {line}");
                            }
                        }
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            Debug.Log($"SseClient: SSE connection for {devId} explicitly cancelled.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"SseClient: Error during SSE connection for {devId}: {ex.Message}");
        }
        finally
        {
            // If connection ends for any reason, ensure _cts is properly reset
            if (_cts != null && _cts.Token == cancellationToken && !cancellationToken.IsCancellationRequested)
            {
                Debug.Log($"SseClient: Disconnecting from SSE for {devId} (explicit cancel or stream end).");
                // Only dispose if it was not cancelled by an explicit StopSseConnection call
                _cts.Dispose();
                _cts = null;
                _currentDevId = string.Empty;
                Debug.Log($"SseClient: Disconnected from SSE for {devId} due to error or natural end of stream.");
            } else if (_cts != null && _cts.Token == cancellationToken) {
                Debug.Log($"SseClient: Disconnecting from SSE for {devId} (already cancelled).");
            }
        }
    }

    private void ProcessSseData(string devId, string jsonData)
    {
        try
        {
            // Deserialize using Newtonsoft.Json
            JObject jsonObject = JObject.Parse(jsonData);
            
            // Extract the "fields" object and convert it to Dictionary<string, JToken>
            if (jsonObject.TryGetValue("fields", out JToken fieldsToken) && fieldsToken.Type == JTokenType.Object)
            {
                Dictionary<string, JToken> fields = fieldsToken.ToObject<Dictionary<string, JToken>>();
                
                // Ensure event is invoked on the main thread
                // Need a UnityMainThreadDispatcher for this
                MainThreadDispatcher.Instance?.RunOnMainThread(() =>
                {
                    OnDiffPatchReceived?.Invoke(devId, fields);
                });
            }
            else
            {
                Debug.LogWarning($"SseClient: DiffPatch data for {devId} missing or invalid 'fields' object.\nRaw: {jsonData}");
            }
        }
        catch (JsonException ex)
        {
            Debug.LogError($"SseClient: JSON parsing error for DiffPatch data for {devId}: {ex.Message}\nRaw: {jsonData}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"SseClient: General error processing DiffPatch data for {devId}: {ex.Message}\nRaw: {jsonData}");
        }
    }
}
