using UnityEngine;
using UnityEngine.InputSystem; 
// We are now using Mouse and Pointer from the InputSystem

public class FirstPersonMovement : MonoBehaviour, @PlayerInputs.IPlayerMovementActions 
{
    // --- STATIC REFERENCE (Singleton Pattern for easy access) ---
    public static FirstPersonMovement Instance { get; private set; } 
    
    // --- ADJUSTABLE SETTINGS ---
    [Header("Movement Settings")]
    public float walkingSpeed = 4.0f;
    
    [Header("Camera Settings")]
    public float lookSpeed = 0.75f;
    
    [Header("Chat/UI Unlock Settings")]
    [Tooltip("Percentage of the screen width that defines the 3D viewport unlock area (e.5 for the left half).")]
    public float viewportUnlockBoundary = 0.5f;

    // --- STATE AND INPUT VARIABLES ---
    private @PlayerInputs playerInputs;
    private CharacterController characterController;
    private Camera mainCamera; 

    public bool IsControlsActive { get; private set; } = false; 
    public bool isMovementLockedByChat = false; 
    
    private Vector2 moveInput;
    private Vector2 lookInput;
    private float rotationX = 0;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        
        playerInputs = new @PlayerInputs();
    }

    void OnEnable()
    {
        playerInputs.PlayerMovement.SetCallbacks(this);
        playerInputs.PlayerMovement.Enable(); 
        
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        SetControlsActive(false); 
    }

    void OnDisable()
    {
        playerInputs.PlayerMovement.Disable(); 
        playerInputs.PlayerMovement.RemoveCallbacks(this); 
    }

    void Start()
    {
        characterController = GetComponent<CharacterController>();
        mainCamera = Camera.main; 
        
        
    }
    
    // -------------------------------------------------------------
    // INPUT CALLBACKS (Used ONLY for Movement/Look/Cancel/Activate)
    // -------------------------------------------------------------
    
    public void OnMove(InputAction.CallbackContext context)
    {
        moveInput = context.performed ? context.ReadValue<Vector2>() : Vector2.zero;
    }

    public void OnLook(InputAction.CallbackContext context)
    {
        lookInput = context.performed ? context.ReadValue<Vector2>() : Vector2.zero;
    }
    
    // This action remains for UNLOCKED activation only.
    public void OnActivate(InputAction.CallbackContext context)
    {
        if (!context.performed) return;

        // Only allow the input action to activate if the chat is not currently forcing a lock.
        if (!IsControlsActive && !isMovementLockedByChat)
        {
            SetControlsActive(true);
        }
    }
    
    public void OnCancel(InputAction.CallbackContext context)
    {
        if (context.performed && IsControlsActive)
        {
            SetControlsActive(false);
        }
    }
    
    // -------------------------------------------------------------
    // MOVEMENT AND UNLOCK LOGIC (Update)
    // -------------------------------------------------------------

    void Update()
    {
        HandleUnlockClickNative(); // NEW Native Input System check
        HandleRotation();
        HandleMovement();
    }

    private void HandleUnlockClickNative()
    {
        // 1. Check if movement is currently locked by the chat UI.
        if (!isMovementLockedByChat) return;
        
        // Ensure Mouse is available and check for LMB press (active input).
        var mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;

        // Read position directly from the Mouse input device.
        Vector2 mousePosition = mouse.position.ReadValue();
        float screenWidthUnlockLimit = Screen.width * viewportUnlockBoundary;


        // 2. Check if the click is on the left side.
        if (mousePosition.x < screenWidthUnlockLimit)
        {

            // Optional Raycast Check: Check if the click hit a scene object.
            if (mainCamera != null)
            {
                Ray ray = mainCamera.ScreenPointToRay(mousePosition);
                if (!Physics.Raycast(ray))
                {
                    return; 
                }
            }
            
            // 3. Unlock movement via ChatFocusHandler
            ChatFocusHandler chatHandler = FindObjectOfType<ChatFocusHandler>();
            if (chatHandler != null)
            {
                chatHandler.SetChatActiveState(false);
            }
        }
    }

    // -------------------------------------------------------------
    // CHAT/UI CONTROL METHOD
    // -------------------------------------------------------------

    public void SetMovementLock(bool isLocked)
    {
        isMovementLockedByChat = isLocked;
        
        if (isLocked)
        {
            // Lock Movement and disable Left Click activation
            SetControlsActive(false);
            playerInputs.PlayerMovement.Activate.Disable(); 
        }
        else
        {
            // Unlock Movement and re-enable Left Click activation
            playerInputs.PlayerMovement.Activate.Enable();
            SetControlsActive(true); 
        }
    }


    // -------------------------------------------------------------
    // CORE STATE MANAGEMENT
    // -------------------------------------------------------------

    private void SetControlsActive(bool isActive)
    {
        if (isMovementLockedByChat && isActive) 
        {
            return;
        }

        IsControlsActive = isActive; 

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true; 

        if (isActive)
        {
            // Debug.Log is handled by calling functions
        }
        else
        {
            moveInput = Vector2.zero;
            lookInput = Vector2.zero;
            // Debug.Log is handled by calling functions
        }
    }

    // -------------------------------------------------------------
    // MOVEMENT AND ROTATION LOGIC
    // -------------------------------------------------------------

    private void HandleRotation()
    {
        if (!IsControlsActive)
        {
            return;
        }

        transform.Rotate(0, lookInput.x * lookSpeed, 0);

        rotationX -= lookInput.y * lookSpeed;
        rotationX = Mathf.Clamp(rotationX, -90f, 90f); 
        Camera.main.transform.localRotation = Quaternion.Euler(rotationX, 0, 0);
    }

    private void HandleMovement()
    {
        if (!IsControlsActive)
        {
            return;
        }

        Vector3 forwardMovement = transform.forward * moveInput.y;
        Vector3 rightMovement = transform.right * moveInput.x;
        Vector3 desiredMove = (forwardMovement + rightMovement).normalized * walkingSpeed;
        
        float yStick = 0;
        if (characterController.isGrounded)
        {
             yStick = -0.1f; 
        }
        
        Vector3 finalMove = new Vector3(desiredMove.x, yStick, desiredMove.z);
        characterController.Move(finalMove * Time.deltaTime);
    }
}