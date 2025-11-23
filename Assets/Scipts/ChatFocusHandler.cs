// ChatFocusHandler.cs (FULL CODE REPLACEMENT)

using UnityEngine;
using TMPro;
// REMOVED: using UnityEngine.EventSystems; // No longer needed
// REMOVED: , IPointerClickHandler // No longer needed

/// <summary>
/// Handles chat state communication with the player controller.
/// </summary>
public class ChatFocusHandler : MonoBehaviour
// NO INTERFACE IMPLEMENTED
{
    [Tooltip("The TMP_InputField component used for chat input.")]
    [SerializeField]
    private TMP_InputField chatInputField;
    
    private ChatManager chatManager; 

    void Start()
    {
        if (chatInputField == null)
        {
            chatInputField = GetComponentInChildren<TMP_InputField>();
        }
        chatManager = GetComponentInParent<ChatManager>(); 
        
        if (chatInputField == null)
        {
            Debug.LogError("ChatFocusHandler Error: chatInputField reference is missing!");
        }
    }

    // REMOVED: OnPointerClick method.

    /// <summary>
    /// Public method to control the chat/movement state toggle.
    /// This is called by ChatManager (on open/close) and by the Input Field (on select).
    /// </summary>
    /// <param name="isActive">True to focus chat/lock movement, False to enable movement.</param>
    public void SetChatActiveState(bool isActive)
    {
        if (FirstPersonMovement.Instance == null)
        {
            Debug.LogError("ChatFocusHandler: FirstPersonMovement instance not found! Movement control disabled.");
            return;
        }

        if (chatInputField == null) return;

        if (isActive)
        {
            // Lock Movement and Focus Input (ChatManager/Input Field clicked)
            FirstPersonMovement.Instance.SetMovementLock(true);
            chatInputField.interactable = true;
            chatInputField.Select();
            chatInputField.ActivateInputField();
            Debug.Log("Chat Focused: Movement Locked.");
        }
        else
        {
            // Unlock Movement (Now only called by the external scene click in FPM)
            FirstPersonMovement.Instance.SetMovementLock(false);
            // We intentionally do NOT DeactivateInputField here, as the chat is still visible.
            Debug.Log("Movement Enabled: Chat De-focused.");
        }
    }
}