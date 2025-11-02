using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using Convai.Scripts.Runtime.Core;
using Convai.Scripts.Runtime.Features;
using Convai.Scripts.Runtime.Utils;
using Service;
using Grpc.Core;
using System.Threading.Tasks;
using UnityEngine.InputSystem;
using static Service.GetResponseRequest.Types;

/// <summary>
/// A self-contained script to manage a text-only chat UI with Convai.
/// This version includes proper session initialization and auto-scrolling.
/// </summary>
public class ConvaiChatClient : MonoBehaviour
{
    [Header("Convai Setup")]
    [Tooltip("The Character ID of the AI personality to talk to.")]
    public string characterId = "";

    [Header("UI References")]
    public TMP_InputField inputField;
    public Button sendButton;
    public TMP_Text chatHistoryText;
    // RE-ADDED: Reference to the ScrollRect so we can access AutoScroll
    public ScrollRect scrollRect; 

    // Private Convai connection variables
    private ConvaiGRPCAPI _grpcApi;
    private ConvaiService.ConvaiServiceClient _client;
    private Channel _channel;
    private string _sessionID = "-1";
    private readonly List<string> _history = new();
    private const string AI_NAME = "AI Assistant";
    private const string GRPC_API_ENDPOINT = "stream.convai.com";

    private bool _isInitialized;
    // NEW: Reference to the AutoScroll script
    private AutoScroll _autoScroll;

    async void Start()
    {
        _grpcApi = FindFirstObjectByType<ConvaiGRPCAPI>();

        if (_grpcApi == null)
        {
            Debug.LogError("ConvaiChatClient Error: ConvaiGRPCAPI not found in the scene.");
            enabled = false;
            return;
        }
        
        // NEW: Get the AutoScroll component from the ScrollRect's GameObject
        if (scrollRect != null)
        {
            _autoScroll = scrollRect.GetComponent<AutoScroll>();
            if (_autoScroll == null)
            {
                Debug.LogWarning("AutoScroll script not found on the ScrollRect GameObject. Auto-scrolling will be disabled.");
            }
        }
        else
        {
            Debug.LogError("ScrollRect reference is missing in the Inspector. Auto-scrolling cannot be set up.");
        }
        // ---

        if (string.IsNullOrEmpty(characterId))
        {
            Debug.LogError("ConvaiChatClient Error: Character ID is not set in the Inspector.");
            enabled = false;
            return;
        }

        if (ConvaiAPIKeySetup.GetAPIKey(out string loadedApiKey))
        {
            Debug.Log($"Attempting to initialize with API Key: {loadedApiKey}");
        }
        else
        {
            Debug.LogError("Could not load API Key from Resources. Please check that the 'Convai API Key' file exists in 'Assets/Convai/Resources' and has the key set.");
        }

        try
        {
            SslCredentials credentials = new SslCredentials();
            List<ChannelOption> options = new() { new(ChannelOptions.MaxReceiveMessageLength, 16 * 1024 * 1024) };
            _channel = new Channel(GRPC_API_ENDPOINT, credentials, options);
            _client = new ConvaiService.ConvaiServiceClient(_channel);

            Debug.Log("Initializing session...");
            _sessionID = await ConvaiGRPCAPI.InitializeSessionIDAsync("ChatClient", _client, characterId, _sessionID);

            if (_sessionID == "-1" || _sessionID == null)
            {
                Debug.LogError("Failed to initialize session. The server rejected the connection. VERIFY your API Key and Character ID are correct.", this);
                AppendMessage(AI_NAME, "Error: Could not connect to the character. Please check the API Key and Character ID.", isError: true);
                return;
            }

            Debug.Log($"Session initialized successfully. Session ID: {_sessionID}");
            _isInitialized = true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"ConvaiChatClient Error: Failed to initialize gRPC client. Error: {e.Message}");
            enabled = false;
            return;
        }

        sendButton?.onClick.AddListener(SendMessage);
        inputField?.onSubmit.AddListener(OnInputSubmit);
        if (chatHistoryText != null) chatHistoryText.text = "";
    }

    async void OnDestroy()
    {
        if (sendButton != null) sendButton.onClick.RemoveListener(SendMessage);
        if (inputField != null) inputField.onSubmit.RemoveListener(OnInputSubmit);
        if (_grpcApi != null) _grpcApi.OnResultReceived -= HandleResultReceivedEvent;
        if (_channel != null) await _channel.ShutdownAsync();
    }

    private void OnInputSubmit(string text)
    {
        if (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame)
        {
            SendMessage();
        }
    }

    private void SendMessage()
    {
        if (!_isInitialized)
        {
            Debug.LogWarning("SendMessage called before session was initialized.");
            return;
        }

        string userText = inputField.text.Trim();
        if (string.IsNullOrEmpty(userText)) return;

        AppendMessage("You", userText);
        inputField.text = "";
        inputField.interactable = false;
        sendButton.interactable = false;

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
                    GetResponseConfig = new GetResponseConfig
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
                    GetResponseData = new GetResponseData { TextData = userText }
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
            Debug.LogError($"RPC Error during SendTextDataAsync: {ex}");
            MainThreadDispatcher.Instance.RunOnMainThread(() => HandleAiResponse($"Error: {ex.Status.Detail}", true));
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
             HandleAiResponse(result.DebugLog, isError: true);
             return;
        }

        if (result.AudioResponse != null)
        {
            if (result.AudioResponse.EndOfResponse)
            {
                HandleAiResponse(result.AudioResponse.TextData);
            }
        }
    }

    private void HandleAiResponse(string responseText, bool isError = false)
    {
        inputField.interactable = true;
        sendButton.interactable = true;
        inputField.Select();
        inputField.ActivateInputField();

        if (!string.IsNullOrEmpty(responseText))
        {
            AppendMessage(AI_NAME, responseText.Trim(), isError);
        }
        else if (!isError)
        {
             AppendMessage(AI_NAME, "Received an empty response from the AI.", isError: true);
        }
    }

    private void AppendMessage(string speaker, string message, bool isError = false)
    {
        string colorTag = speaker == "You" ? "#88CCFF" : "#FFC0CB";
        if (isError) colorTag = "#FF5555";

        string formattedMessage = $"<color={colorTag}><b>{speaker}:</b></color> {message}";

        _history.Add(formattedMessage);
        chatHistoryText.text = string.Join("\n\n", _history);

        // NEW: Call the ScrollToBottom method from the AutoScroll script
        if (_autoScroll != null)
        {
            _autoScroll.ScrollToBottom();
        }
    }
}