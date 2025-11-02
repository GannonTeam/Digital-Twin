using UnityEngine;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Convai.Scripts.Runtime.Core;
using Convai.Scripts.Runtime.Features; 
using Service; 
using Grpc.Core;
using Convai.Scripts.Runtime.Utils; 

/// <summary>
/// Handles the core gRPC connection and text streaming with the Convai API.
/// </summary>
public class ConvaiTextService : MonoBehaviour
{
    [Header("Convai Setup")]
    [Tooltip("The Character ID of the AI personality to talk to.")]
    public string characterId = "";

    // Action to notify the ChatManager when a final response is ready.
    public Action<string, bool> OnTextResponseReceived;

    private ConvaiService.ConvaiServiceClient _client;
    private Channel _channel;
    private string _sessionID = "-1";
    private const string GRPC_API_ENDPOINT = "stream.convai.com";
    private bool _isInitialized;
    
    // Flag to ensure the final response event is only fired once per query.
    private bool _responseSent; 

    public bool IsInitialized => _isInitialized;

    async void Start()
    {
        if (string.IsNullOrEmpty(characterId))
        {
            Debug.LogError("ConvaiTextService Error: Character ID is not set.");
            enabled = false;
            return;
        }

        if (!ConvaiAPIKeySetup.GetAPIKey(out string loadedApiKey))
        {
            Debug.LogError("ConvaiTextService Error: Could not load API Key.");
        }

        try
        {
            SslCredentials credentials = new SslCredentials();
            List<ChannelOption> options = new() { new(ChannelOptions.MaxReceiveMessageLength, 16 * 1024 * 1024) };
            _channel = new Channel(GRPC_API_ENDPOINT, credentials, options);
            _client = new ConvaiService.ConvaiServiceClient(_channel);

            _sessionID = await ConvaiGRPCAPI.InitializeSessionIDAsync("ChatClient", _client, characterId, _sessionID);

            if (_sessionID == "-1" || _sessionID == null)
            {
                OnTextResponseReceived?.Invoke("Error: Could not connect to the character.", true);
                return;
            }

            _isInitialized = true;
        }
        catch (System.Exception e)
        {
            OnTextResponseReceived?.Invoke($"Error: Failed to initialize gRPC client: {e.Message}", true);
            enabled = false;
        }
    }

    async void OnDestroy()
    {
        if (_channel != null) await _channel.ShutdownAsync();
    }

    /// <summary>
    /// Sends a text query to the Convai API.
    /// </summary>
    public void SendTextQuery(string userText)
    {
        if (!_isInitialized)
        {
            OnTextResponseReceived?.Invoke("AI is not connected.", true);
            return;
        }
        
        _responseSent = false; // Reset the flag for the new query
        Task.Run(() => SendTextDataAsync(userText));
    }

    private async Task SendTextDataAsync(string userText)
    {
        try
        {
            using (var call = _client.GetResponse())
            {
                ConvaiAPIKeySetup.GetAPIKey(out string apiKey);

                GetResponseRequest getResponseConfigRequest = new()
                {
                    GetResponseConfig = new GetResponseRequest.Types.GetResponseConfig
                    {
                        CharacterId = this.characterId,
                        ApiKey = apiKey,
                        SessionId = this._sessionID,
                        AudioConfig = new AudioConfig { DisableAudio = true }
                    }
                };

                await call.RequestStream.WriteAsync(getResponseConfigRequest);
                
                await call.RequestStream.WriteAsync(new GetResponseRequest
                {
                    GetResponseData = new GetResponseRequest.Types.GetResponseData { TextData = userText }
                });

                await call.RequestStream.CompleteAsync();

                while (await call.ResponseStream.MoveNext())
                {
                    GetResponseResponse result = call.ResponseStream.Current;
                    MainThreadDispatcher.Instance.RunOnMainThread(() => HandleResultReceivedEvent(result));
                }
            }
        }
        catch (RpcException ex)
        {
            MainThreadDispatcher.Instance.RunOnMainThread(() => OnTextResponseReceived?.Invoke($"Network Error: {ex.Status.Detail}", true));
        }
    }

    private void HandleResultReceivedEvent(GetResponseResponse result)
    {
        if (!string.IsNullOrEmpty(result.SessionId))
        {
            _sessionID = result.SessionId;
        }

        if (result.DebugLog != null && result.AudioResponse == null)
        {
            OnTextResponseReceived?.Invoke(result.DebugLog, true);
            return;
        }

        if (result.AudioResponse != null)
        {
            if (result.AudioResponse.EndOfResponse)
            {
                // Ignore empty end-of-response messages
                if (string.IsNullOrEmpty(result.AudioResponse.TextData)) return;

                // DIAGNOSTIC CHECK: Is the second message blocked?
                if (_responseSent) 
                {
                    Debug.LogWarning("Convai Service: Duplicate EndOfResponse BLOCKED by _responseSent flag.");
                    return; 
                } 
                
                _responseSent = true;
                
                Debug.Log("Convai Service: Invoking final response event."); // <- PRIMARY LOG

                // Clean up the text by removing the visible HTML-like tags
                string cleanResponseText = result.AudioResponse.TextData
                    .Trim()
                    .Replace("</response>", "")
                    .Trim();
                
                OnTextResponseReceived?.Invoke(cleanResponseText, false);
            }
        }
    }
}