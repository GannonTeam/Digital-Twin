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
    [Header("Backend")]
    [Tooltip("Base URL, e.g. https://digitwinbackend.quangphuly.online")]
    public string backendBaseUrl = "https://digitwinbackend.quangphuly.online";

    [Tooltip("Endpoint path for full list, e.g. /api/printers")]
    public string endpointPath = "/api/printers";

    [Tooltip("Optional format for per-printer endpoint. Use {id} where the printer id should go. Example: /api/printers/{id}")]
    public string perIdEndpointFormat = ""; // leave empty to use endpointPath + "/{id}"

    [Header("Polling")]
    [Tooltip("Seconds between successful polls.")]
    public float pollInterval = 0.5f;

    [Tooltip("If true, polling will start automatically on Start().")]
    public bool startOnAwake = true;

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
        if (isPolling) return;
        pollCoroutine = StartCoroutine(PollLoop());
        Debug.Log("PrinterPollingClient: Started polling " + CombineUrl(backendBaseUrl, endpointPath));
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
        yield return DoRequestAndDeliver(CombineUrl(backendBaseUrl, endpointPath));
    }

    /// <summary>
    /// Request a single printer's state. Uses perIdEndpointFormat if provided, otherwise appends "/{printerId}" to endpointPath.
    /// Starts an independent coroutine to fetch the single resource and deliver it to DashboardManager.
    /// If your backend does not support per-id GET, do not call this (DashboardManager will request full list via RequestOnce()).
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

        string path;
        if (!string.IsNullOrEmpty(perIdEndpointFormat))
        {
            path = perIdEndpointFormat.Replace("{id}", encodedId);
        }
        else
        {
            // ensure we don't double slash
            string basePath = endpointPath;
            if (basePath.EndsWith("/")) basePath = basePath.TrimEnd('/');
            path = basePath + "/" + encodedId;
        }

        string url = CombineUrl(backendBaseUrl, path);
        StartCoroutine(DoRequestAndDeliver(url));
    }

    private IEnumerator PollLoop()
    {
        string url = CombineUrl(backendBaseUrl, endpointPath);

        while (true)
        {
            yield return DoRequestAndDeliver(url).WrapWithUnityCoroutine();
            yield return new WaitForSeconds(pollInterval);
        }
    }

    // Helper to actually perform the HTTP GET and deliver the JSON to DashboardManager.
    private IEnumerator DoRequestAndDeliver(string url)
    {
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
                if (string.IsNullOrEmpty(json))
                {
                    Debug.LogWarning("PrinterPollingClient: empty JSON response.");
                }
                else
                {
                    try
                    {
                        if (DashboardManager.Instance != null)
                        {
                            // DashboardManager now safely handles cross-thread calls; we're on main thread here (coroutine).
                            DashboardManager.Instance.ReceivePrinterJson(json);
                        }
                        else
                        {
                            Debug.LogError("PrinterPollingClient: DashboardManager.Instance is null. Ensure DashboardManager is present in the scene.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError("PrinterPollingClient: JSON/Delivery exception: " + ex);
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

/// <summary>
/// Small extension to make starting IEnumerator inline easier (compat shim).
/// This extension simply yields the passed IEnumerator (no extra behavior).
/// </summary>
public static class CoroutineExtensions
{
    public static IEnumerator WrapWithUnityCoroutine(this IEnumerator enumerator)
    {
        while (enumerator.MoveNext())
        {
            yield return enumerator.Current;
        }
    }
}