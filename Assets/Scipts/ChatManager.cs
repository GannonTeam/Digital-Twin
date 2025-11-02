using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.InputSystem; 

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
    
    // FINAL FIX: Field to store the last bot message to prevent immediate duplicates
    private string _lastBotMessageDisplayed = ""; 
    // --------------------------------------------------------------------------

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
    }

    private void OnEnable()
    {
        if (sendInputMessageAction != null)
        {
            sendInputMessageAction.performed += OnSendInputPerformed;
            sendInputMessageAction.Enable();
        }

        // FIX: Defensive Subscription to prevent duplicate event firing.
        if (convaiService != null)
        {
            convaiService.OnTextResponseReceived -= OnConvaiResponseReceived; // Unsubscribe defensively
            convaiService.OnTextResponseReceived += OnConvaiResponseReceived; // Subscribe
        } 
    }

    private void OnDisable()
    {
        if (sendInputMessageAction != null)
        {
            sendInputMessageAction.performed -= OnSendInputPerformed;
            sendInputMessageAction.Disable();
        }

        // Clean Unsubscribe
        if (convaiService != null)
        {
            convaiService.OnTextResponseReceived -= OnConvaiResponseReceived;
        }
    }

    private void OnSendInputPerformed(InputAction.CallbackContext context)
    {
        if (!string.IsNullOrWhiteSpace(inputField.text))
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

        DisplayMessage("Welcome to the Convai Assistant Chat!", MessageSource.Bot);
        isChatInitialized = true;
    }

    /// <summary>
    /// Displays a message in the chat panel.
    /// </summary>
    public void DisplayMessage(string messageContent, MessageSource source)
    {
        if (string.IsNullOrWhiteSpace(messageContent)) return;
        
        // --- FINAL FIX: DEDUPLICATION CHECK ---
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
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentPanel.GetComponent<RectTransform>());
        }
        
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
        DisplayMessage(responseText, MessageSource.Bot);
        
        inputField.interactable = true;
        inputField.Select();
        inputField.ActivateInputField();
    }
}