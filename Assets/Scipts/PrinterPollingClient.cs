using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using System;

/// <summary>
/// Polls the backend for printer state JSON and forwards it to DashboardManager.
/// Added: RequestSingle(printerId) which requests a single-printer endpoint if available.
/// - Calls DashboardManager.Instance.ReceivePrinterJson(json) on success.
/// - Public StartPolling/StopPolling/RequestOnce/RequestSingle methods for runtime control.
/// </summary>
public class PrinterPollingClient : MonoBehaviour
{
    // --- Hardcoded Backend Configuration ---
    private const string BackendBaseUrl = "https://digitwinbackend.quangphuly.online";
    private const string ListEndpointPath = "/twin/printers"; 
    private const string PerIdEndpointFormat = "/twin/printers/{id}";
    private const float DefaultPollInterval = 0.5f;
    // ---------------------------------------

    [Header("Polling")]
    [Tooltip("If true, polling will start automatically on Start().")]
    public bool startOnAwake = true;
    
    // We keep these public as they relate to authorization and custom headers,
    // which might need dynamic assignment or testing.
    [Header("Auth / Headers")]
    [Tooltip("Optional bearer token for Authorization header.")]
    public string bearerToken = "";

    [Tooltip("Optional additional header in the format 'Name:Value'. Leave empty to skip.")]
    public string extraHeader = "";

    // internal state
    private Coroutine pollCoroutine;
    private bool isPolling => pollCoroutine != null;

    void Start()
    {
        if (startOnAwake) StartPolling();
    }

    void OnDisable()
    {
        StopPolling();
    }

    /// <summary>
    /// Start the polling coroutine if not already running.
    /// </summary>
    public void StartPolling()
    {
        // Add a check to prevent errors if DashboardManager hasn't initialized yet
        if (DashboardManager.Instance == null)
        {
             Debug.LogError("DashboardManager not initialized. Cannot start polling.");
             return;
        }

        if (isPolling) return;
        pollCoroutine = StartCoroutine(PollLoop());
        Debug.Log("PrinterPollingClient: Started polling " + CombineUrl(BackendBaseUrl, ListEndpointPath));
    }

    /// <summary>
    /// Stop polling.
    /// </summary>
    public void StopPolling()
    {
        if (!isPolling) return;
        StopCoroutine(pollCoroutine);
        pollCoroutine = null;
        Debug.Log("PrinterPollingClient: Stopped polling.");
    }

    /// <summary>
    /// Perform a single immediate request for the full list (does not affect the running poll loop).
    /// </summary>
    public void RequestOnce()
    {
        StartCoroutine(RequestOnceCoroutine());
    }

    private IEnumerator RequestOnceCoroutine()
    {
        // Uses the hardcoded ListEndpointPath
        yield return DoRequestAndDeliver(CombineUrl(BackendBaseUrl, ListEndpointPath));
    }

    /// <summary>
    /// Request a single printer's state.
    /// </summary>
    public void RequestSingle(string printerId)
    {
        if (string.IsNullOrEmpty(printerId)) return;

        // Build per-id path
        string encodedId;
        try
        {
            encodedId = UnityWebRequest.EscapeURL(printerId);
        }
        catch
        {
            encodedId = printerId; // fallback
        }

        // Uses the hardcoded PerIdEndpointFormat
        string path = PerIdEndpointFormat.Replace("{id}", encodedId);
        
        string url = CombineUrl(BackendBaseUrl, path);
        StartCoroutine(DoRequestAndDeliver(url));
    }

    private IEnumerator PollLoop()
    {
        string url = CombineUrl(BackendBaseUrl, ListEndpointPath);

        while (true)
        {
            yield return DoRequestAndDeliver(url);
            yield return new WaitForSeconds(DefaultPollInterval); // Uses the hardcoded interval
        }
    }

    // Helper to actually perform the HTTP GET and deliver the JSON to DashboardManager.
    private IEnumerator DoRequestAndDeliver(string url)
    {
        // LOG 1: Show the URL being requested
        Debug.Log($"[POLL REQUEST] Sending GET to: {url}");
        
        using (UnityWebRequest www = UnityWebRequest.Get(url))
        {
            www.SetRequestHeader("Accept", "application/json");
            if (!string.IsNullOrEmpty(bearerToken))
                www.SetRequestHeader("Authorization", "Bearer " + bearerToken);

            if (!string.IsNullOrEmpty(extraHeader))
            {
                // extraHeader expected in "Name:Value" form
                var idx = extraHeader.IndexOf(':');
                if (idx > 0 && idx < extraHeader.Length - 1)
                {
                    string name = extraHeader.Substring(0, idx).Trim();
                    string value = extraHeader.Substring(idx + 1).Trim();
                    if (!string.IsNullOrEmpty(name)) www.SetRequestHeader(name, value);
                }
            }

            yield return www.SendWebRequest();

#if UNITY_2020_1_OR_NEWER
            bool isError = www.result == UnityWebRequest.Result.ConnectionError || www.result == UnityWebRequest.Result.ProtocolError;
#else
            bool isError = www.isNetworkError || www.isHttpError;
#endif

            if (isError)
            {
                string respCode = www.responseCode > 0 ? www.responseCode.ToString() : "n/a";
                Debug.LogWarning($"PrinterPollingClient: request error ({respCode}) - {www.error}. URL: {url}");
            }
            else
            {
                string json = www.downloadHandler.text;
                
                // LOG 2: Show the raw data received on success
                Debug.Log($"[POLL RESPONSE] URL: {url}\nRaw Data: {json}");

                if (string.IsNullOrEmpty(json))
                {
                    Debug.LogWarning("PrinterPollingClient: empty JSON response.");
                }
                else
                {
                    try
                    {
                        // The JSON data is passed to the DashboardManager for processing (deserialization).
                        if (DashboardManager.Instance != null)
                        {
                            DashboardManager.Instance.ReceivePrinterJson(json);
                        }
                        else
                        {
                            // This error should be caught in StartPolling, but remains here as a fallback
                            Debug.LogError("PrinterPollingClient: DashboardManager.Instance is null. Ensure DashboardManager is present in the scene.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError("PrinterPollingClient: JSON/Delivery exception: " + ex + "\nRaw: " + json);
                    }
                }
            }
        }
    }

    private string CombineUrl(string baseUrl, string path)
    {
        if (string.IsNullOrEmpty(baseUrl)) return path;
        if (baseUrl.EndsWith("/")) baseUrl = baseUrl.TrimEnd('/');
        if (!path.StartsWith("/")) path = "/" + path;
        return baseUrl + path;
    }
}