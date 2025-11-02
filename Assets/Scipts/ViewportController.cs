using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Controls the camera viewport size and the activation of a corresponding UI element
/// based on the selected DisplayMode (General Panel or Chat Canvas).
/// </summary>
public class ViewportController : MonoBehaviour
{
    // === STATIC TRACKING ===
    public static ViewportController CurrentActiveController { get; private set; }

    /// <summary>
    /// Defines which UI element this specific controller instance should manage.
    /// </summary>
    public enum DisplayMode {
        GeneralPanel, // Uses the 'uiPanel' reference (e.g., Log/Dashboard)
        ChatCanvas    // Uses the 'chatCanvas' reference (e.g., Convai Chat)
    }

    [Header("Configuration")]
    [SerializeField]
    private DisplayMode displayMode = DisplayMode.GeneralPanel;

    [Header("Input and UI References")]
    [SerializeField]
    private InputActionReference clickAction; 

    [SerializeField]
    private GameObject uiPanel; 

    [SerializeField]
    private GameObject chatCanvas; 
    
    // NEW: Cache the ChatManager component if this controller opens the chat.
    private ChatManager cachedChatManager;

    // --- Private State ---
    private Camera mainCamera;
    private bool isViewShrunk = false;

    // --- Viewport Constants ---
    private static readonly Rect FullViewRect = new Rect(0f, 0f, 1f, 1f); 
    private static readonly Rect ShrunkViewRect = new Rect(0f, 0f, 0.6f, 1f); 

    private void Awake()
    {
        if (clickAction?.action != null)
        {
            clickAction.action.performed += OnClickPerformed; 
        }
    }

    private void OnDestroy()
    {
        if (clickAction?.action != null)
        {
            clickAction.action.performed -= OnClickPerformed;
        }
        
        if (CurrentActiveController == this)
        {
            CurrentActiveController = null;
        }
    }

    private void Start()
    {
        if (Camera.main != null)
        {
            mainCamera = Camera.main.GetComponent<Camera>();
        }

        if (mainCamera == null)
        {
            Debug.LogError("ViewportController Error: Main Camera (with 'MainCamera' tag) not found or missing Camera component.");
            enabled = false;
        }
        
        // --- RELOCATED CACHING LOGIC ---
        // Cache the ChatManager reference here, which runs after all Awake() calls.
        if (displayMode == DisplayMode.ChatCanvas && chatCanvas != null)
        {
            // Use GetComponentInChildren for flexibility, allowing ChatManager to be on the parent or a child panel.
            cachedChatManager = chatCanvas.GetComponentInChildren<ChatManager>(true);
            if (cachedChatManager == null)
            {
                Debug.LogError("ViewportController Error: ChatManager component not found on the assigned Chat Canvas object or its children.");
            }
        }
        // --- END RELOCATED CACHING LOGIC ---

        // Use the CloseViewportView(true) override to ensure initial state is full view and panels are hidden.
        CloseViewportView(true); 

        if (clickAction?.action != null)
        {
            clickAction.action.Enable();
        }
    }

    private void OnClickPerformed(InputAction.CallbackContext context)
    {
        if (mainCamera == null || Mouse.current == null) return;
        
        Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            if (hit.collider.gameObject == gameObject)
            {
                // Check if this controller is already active for toggling.
                if (CurrentActiveController == this && isViewShrunk)
                {
                    CloseViewportView();
                }
                else
                {
                    OpenViewportView();
                }
            }
        }
    }

    /// <summary>
    /// Public function to explicitly open the view (shrink camera, show panel).
    /// </summary>
    public void OpenViewportView()
    {
        if (mainCamera == null) return; 

        // 1. If another controller is currently open, close it first.
        if (CurrentActiveController != null && CurrentActiveController != this)
        {
            // Use CloseViewportView(false) to prevent restoring camera twice.
            CurrentActiveController.CloseViewportView(false); 
        }
        
        // 2. Set THIS instance as the currently active controller.
        CurrentActiveController = this;
        
        // 3. Shrink the view.
        isViewShrunk = true;
        mainCamera.rect = ShrunkViewRect;

        // 4. Activate the correct panel and hide the other
        if (displayMode == DisplayMode.GeneralPanel)
        {
            uiPanel?.SetActive(true);
            chatCanvas?.SetActive(false);
        }
        else // DisplayMode.ChatCanvas
        {
            uiPanel?.SetActive(false);
            chatCanvas?.SetActive(true);
            
            // CALL CHAT INITIALIZATION
            cachedChatManager?.InitializeChat(); 
        }
    }
    
    /// <summary>
    /// Public function to explicitly close the view (restore camera, hide panel).
    /// </summary>
    /// <param name="forceClose">If true, forces the camera to restore to FullViewRect (used in Start).</param>
    public void CloseViewportView(bool forceClose = false)
    {
        if (mainCamera == null) return;

        // Only proceed if this controller owns the view OR we are forcing a close
        if (isViewShrunk || forceClose)
        {
            isViewShrunk = false;
            
            // Restore the view and hide BOTH potential panels.
            mainCamera.rect = FullViewRect;
            uiPanel?.SetActive(false);
            chatCanvas?.SetActive(false);
            
            // Clear the static reference, since the view is now closed.
            if (CurrentActiveController == this)
            {
                CurrentActiveController = null;
            }
        }
    }
    
    /// <summary>
    /// STATIC method called by the UI Close Button. 
    /// </summary>
    public static void StaticClose()
    {
        if (CurrentActiveController != null)
        {
            CurrentActiveController.CloseViewportView();
        }
    }
}
