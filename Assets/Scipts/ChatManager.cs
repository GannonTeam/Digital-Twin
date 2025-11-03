using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.InputSystem; 
using System; 

/// <summary>
/// Data structure to hold information for a single chat message.
/// </summary>
public class ChatMessage
{
    public string Content;
    public TextMeshProUGUI TextObject; 
    public MessageSource Source;
}

/// <summary>
/// Defines who the message came from.
/// </summary>
public enum MessageSource
{
    User, 
    Bot
}

/// <summary>
/// Manages the chat functionality, including displaying messages and handling input,
/// and integrating with the ConvaiTextService for AI responses.
/// </summary>
public class ChatManager : MonoBehaviour
{
    [Header("UI References")]
    public Transform contentPanel;          
    public GameObject messageTextPrefab;      
    public TMP_InputField inputField;           
    public ScrollRect scrollView;

    [Header("Configuration")]
    public Color UserColor = new Color32(50, 200, 255, 255); 
    public Color BotColor = Color.white;
    public int MaxMessages = 25;

    // --- CONVAI INTEGRATION: Private Reference ---
    private ConvaiTextService convaiService;
    // ---------------------------------------------
    
    // Field to store the last bot message to prevent immediate duplicates
    private string _lastBotMessageDisplayed = ""; 
    
    // NEW ROBUST FIX: Cooldown timer to prevent processing duplicate responses.
    private float lastResponseTime = 0f;
    private const float RESPONSE_COOLDOWN = 0.5f; // Ignore any response within 0.5s of the last valid one.

    private List<ChatMessage> messageHistory = new List<ChatMessage>();
    private bool isChatInitialized = false; 
    private InputAction sendInputMessageAction; 

    // --- Input System Setup ---

    private void Awake()
    {
        // Setup input action bindings
        sendInputMessageAction = new InputAction("SendChatMessage", type: InputActionType.Button);
        sendInputMessageAction.AddBinding("<Keyboard>/enter");
        sendInputMessageAction.AddBinding("<Keyboard>/numpadEnter");

        // Auto-detect the service once.
        convaiService = FindObjectOfType<ConvaiTextService>(); 
        if (convaiService == null)
        {
            Debug.LogError("ChatManager Fatal Error: ConvaiTextService not found in the scene.");
        }
        
        // Subscribe to the Convai service ONLY once during the component's lifetime (Awake).
        if (convaiService != null)
        {
            convaiService.OnTextResponseReceived -= OnConvaiResponseReceived; // Defensive Unsubscribe
            convaiService.OnTextResponseReceived += OnConvaiResponseReceived; // Single Subscription
        }
    }
    
    private void OnDestroy()
    {
        // Unsubscribe to prevent memory leaks when the component is destroyed.
        if (convaiService != null)
        {
            convaiService.OnTextResponseReceived -= OnConvaiResponseReceived;
        }
    }

    private void OnEnable()
    {
        if (sendInputMessageAction != null)
        {
            sendInputMessageAction.performed += OnSendInputPerformed;
            sendInputMessageAction.Enable();
        }
        
        // Initialize chat here. This ensures the welcome message is created the first time the panel is enabled.
        InitializeChat();
    }

    private void OnDisable()
    {
        if (sendInputMessageAction != null)
        {
            sendInputMessageAction.performed -= OnSendInputPerformed;
            sendInputMessageAction.Disable();
        }
    }

    private void OnSendInputPerformed(InputAction.CallbackContext context)
    {
        // Only handle the message if the input field is currently selected/focused
        if (inputField != null && inputField.isFocused && !string.IsNullOrWhiteSpace(inputField.text))
        {
            HandleUserMessage();
        }
    }

    // --- Core Chat Logic ---

    void Start()
    {
        if (inputField != null)
        {
            inputField.ActivateInputField();
        }
    }

    public void InitializeChat()
    {
        if (isChatInitialized) return; 

        isChatInitialized = true; // Set flag immediately to prevent duplication/race conditions
        DisplayMessage("Welcome to the Convai Assistant Chat!", MessageSource.Bot);
    }

    /// <summary>
    /// Displays a message in the chat panel.
    /// </summary>
    public void DisplayMessage(string messageContent, MessageSource source)
    {
        if (string.IsNullOrWhiteSpace(messageContent)) return;
        
        // --- DEDUPLICATION CHECK ---
        if (source == MessageSource.Bot)
        {
            // Check if the exact same message was just displayed by the bot.
            if (messageContent.Trim().Equals(_lastBotMessageDisplayed.Trim(), System.StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogWarning($"ChatManager: Blocking identical duplicate bot message: '{messageContent}'");
                return; 
            }
            // Update the last displayed message for the next check
            _lastBotMessageDisplayed = messageContent;
        }
        else // User message always clears the cache
        {
            _lastBotMessageDisplayed = ""; 
        }
        // --------------------------------------

        // 1. Enforce message history limit
        if (messageHistory.Count >= MaxMessages)
        {
            Destroy(messageHistory[0].TextObject.gameObject);
            messageHistory.RemoveAt(0);
        }
        
        // 2. Prepare content and formatting
        var newMessage = new ChatMessage { Content = messageContent, Source = source };
        string formattedContent = messageContent;

        if (source == MessageSource.User)
        {
            formattedContent = "<b>You:</b> " + messageContent; 
        } 
        else if (isChatInitialized) // Bot Message with Prefix (after welcome)
        {
            formattedContent = "<b>AI Assistant:</b> " + messageContent; 
        }

        // 3. Instantiate the TextMeshPro object
        var newTextObject = Instantiate(messageTextPrefab, contentPanel); 
        
        // 4. Get the TMP component
        TextMeshProUGUI[] tmps = newTextObject.GetComponentsInChildren<TextMeshProUGUI>(true);

        if (tmps.Length == 0)
        {
            Debug.LogError("DisplayMessage Error: messageTextPrefab is missing a TextMeshProUGUI component.");
            Destroy(newTextObject);
            return;
        }
        
        // Use the first TMP component found as the official text object to control
        newMessage.TextObject = tmps[0]; 
        
        // 5. Configure the text and color
        newMessage.TextObject.text = formattedContent;
        newMessage.TextObject.color = source == MessageSource.User ? UserColor : BotColor;

        messageHistory.Add(newMessage);
        
        // 6. Force UI rebuild and Auto-scroll
        if (contentPanel != null)
        {
            // Force layout rebuild before scrolling
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentPanel.GetComponent<RectTransform>());
        }
        
        // Autoscroll (using manual vertical position set, which is reliable after a forced rebuild)
        if (scrollView != null)
        {
            scrollView.verticalNormalizedPosition = 0f;
        }
    }
    
    public void HandleUserMessage()
    {
        string messageContent = inputField.text;
        
        if (string.IsNullOrWhiteSpace(messageContent)) return; 

        DisplayMessage(messageContent, MessageSource.User);
        inputField.text = "";
        inputField.ActivateInputField(); 
        
        GenerateBotResponse(messageContent);
    }
    
    // --- Convai Communication ---

    public void GenerateBotResponse(string userMessage)
    {
        if (convaiService == null || !convaiService.IsInitialized)
        {
            DisplayMessage("AI service is unavailable.", MessageSource.Bot);
            return;
        }
        
        convaiService.SendTextQuery(userMessage);
    }

    private void OnConvaiResponseReceived(string responseText, bool isError)
    {
        // NEW FIX: Check the cooldown timer. If we just processed a response, ignore this one.
        if (Time.time < lastResponseTime + RESPONSE_COOLDOWN)
        {
            Debug.LogWarning("ChatManager: Blocking duplicate/fast response due to cooldown.");
            return;
        }

        // Record the time of the valid response
        lastResponseTime = Time.time;

        DisplayMessage(responseText, MessageSource.Bot);
        
        inputField.interactable = true;
        inputField.Select();
        inputField.ActivateInputField();
    }
}
