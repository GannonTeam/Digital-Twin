using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections.Generic;

/// <summary>
/// Polls the backend for printer state JSON and forwards it to DashboardManager.
/// </summary>
public class PrinterPollingClient : MonoBehaviour
{
    private const string BackendBaseUrl = "https://digitwinbackend.quangphuly.online";
    private const string ListEndpointPath = "/twin/printers"; 
    private const string PerIdEndpointFormat = "/twin/printers/{id}";

    [Header("Polling")]
    public bool startOnAwake = false;
    public float pollInterval = 30f;
    
    [Header("Auth / Headers")]
    public string bearerToken = "";
    public string extraHeader = "";

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

    public void StartPolling()
    {
        if (DashboardManager.Instance == null)
        {
             Debug.LogError("DashboardManager not initialized. Cannot start polling.");
             return;
        }

        if (isPolling) return;
        pollCoroutine = StartCoroutine(PollLoop());
        Debug.Log("PrinterPollingClient: Started polling.");
    }

    public void StopPolling()
    {
        if (!isPolling) return;
        StopCoroutine(pollCoroutine);
        pollCoroutine = null;
    }

    public void RequestOnce()
    {
        StartCoroutine(DoRequestAndDeliver(CombineUrl(BackendBaseUrl, ListEndpointPath)));
    }

    public void RequestSingle(string printerId)
    {
        if (string.IsNullOrEmpty(printerId)) return;

        string encodedId = UnityWebRequest.EscapeURL(printerId);
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
            yield return new WaitForSeconds(pollInterval);
        }
    }

    private IEnumerator DoRequestAndDeliver(string url)
    {
        using (UnityWebRequest www = UnityWebRequest.Get(url))
        {
            www.SetRequestHeader("Accept", "application/json");
            if (!string.IsNullOrEmpty(bearerToken))
                www.SetRequestHeader("Authorization", "Bearer " + bearerToken);

            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"PrinterPollingClient: Error ({www.responseCode}) - {www.error}");
            }
            else
            {
                string json = www.downloadHandler.text;
                if (string.IsNullOrEmpty(json)) yield break;

                try
                {
                    // FIX: Parse the JSON here and send Objects to DashboardManager
                    // We check if the JSON is an array [] or a single object {}
                    if (json.Trim().StartsWith("["))
                    {
                        // It's a list (from /twin/printers)
                        PrinterState[] states = JsonHelper.FromJson<PrinterState>(json);
                        DashboardManager.Instance.ReceivePrinterStates(states);
                    }
                    else
                    {
                        // It's a single printer (from /twin/printers/{id})
                        PrinterState singleState = JsonUtility.FromJson<PrinterState>(json);
                        DashboardManager.Instance.ReceivePrinterStates(new PrinterState[] { singleState });
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"PrinterPollingClient: Parsing error: {ex.Message}\nRaw: {json}");
                }
            }
        }
    }

    private string CombineUrl(string baseUrl, string path)
    {
        if (string.IsNullOrEmpty(baseUrl)) return path;
        return baseUrl.TrimEnd('/') + "/" + path.TrimStart('/');
    }
}