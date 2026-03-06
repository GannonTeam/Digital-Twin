using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Convai.Scripts.Runtime.Utils;
using UnityEngine;
using Newtonsoft.Json.Linq;

public class SseClient : MonoBehaviour
{
    private const string BackendBaseUrl = "https://digitwinbackend.quangphuly.online";
    // FIXED: Pluralized 'printers' and changed to query parameter '?ids='
    private const string SseEndpointFormat = "/twin/stream/printers?ids={0}"; 

    [Header("Auth")]
    public string bearerToken = "";

    private HttpClient _httpClient;
    private CancellationTokenSource _cts;
    private string _currentDevId = string.Empty;
    public string CurrentDevId => _currentDevId;

    public event Action<string, Dictionary<string, JToken>> OnDiffPatchReceived;

    void Awake()
    {
        _httpClient = new HttpClient();
        // Set a long timeout for the stream itself
        _httpClient.Timeout = TimeSpan.FromMilliseconds(Timeout.Infinite); 
    }

    void OnDestroy()
    {
        StopSseConnection();
        _httpClient?.Dispose();
    }

    public void StartSseConnection(string devId)
    {
        if (_cts != null && _currentDevId == devId) return;

        if (_cts != null) StopSseConnection();

        if (string.IsNullOrEmpty(devId)) return;

        _cts = new CancellationTokenSource();
        _currentDevId = devId;
        
        // Construct URL: /twin/stream/printers?ids=PRN001
        string url = BackendBaseUrl + string.Format(SseEndpointFormat, devId);
        _ = ConnectToSseAsync(url, devId, _cts.Token);
    }

    public void StopSseConnection()
    {
        if (_cts != null)
        {
            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
            _currentDevId = string.Empty;
        }
    }

    private async Task ConnectToSseAsync(string url, string devId, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/event-stream"));

            if (!string.IsNullOrEmpty(bearerToken))
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearerToken);

            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            using var reader = new StreamReader(await response.Content.ReadAsStreamAsync());
            Debug.Log($"SseClient: Connected to {devId}");

            while (!cancellationToken.IsCancellationRequested)
            {
                string line = await reader.ReadLineAsync();
                if (line == null) break;

                if (line.StartsWith("data:"))
                {
                    string jsonData = line.Substring(5).Trim();
                    ProcessSseData(devId, jsonData);
                }
            }
        }
        catch (Exception ex) when (!(ex is OperationCanceledException))
        {
            Debug.LogError($"SseClient Error: {ex.Message}");
        }
    }

    private void ProcessSseData(string devId, string jsonData)
    {
        try
        {
            // FIXED: The backend sends the object directly. 
            // We parse it and convert it to a dictionary for the UI to consume.
            JObject jsonObject = JObject.Parse(jsonData);
            Dictionary<string, JToken> updates = jsonObject.ToObject<Dictionary<string, JToken>>();

            MainThreadDispatcher.Instance?.RunOnMainThread(() =>
            {
                OnDiffPatchReceived?.Invoke(devId, updates);
            });
        }
        catch (Exception ex)
        {
            Debug.LogError($"SseClient JSON Error: {ex.Message}");
        }
    }
}