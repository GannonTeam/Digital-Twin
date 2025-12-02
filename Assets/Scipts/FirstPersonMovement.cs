using UnityEngine;
using UnityEngine.InputSystem; 

public class FirstPersonMovement : MonoBehaviour, @PlayerInputs.IPlayerMovementActions 
{
    // --- STATIC REFERENCE ---
    public static FirstPersonMovement Instance { get; private set; } 
    
    // --- ADJUSTABLE SETTINGS ---
    [Header("Movement Settings")]
    public float walkingSpeed = 4.0f;
    
    [Header("Camera Settings")]
    public float lookSpeed = 0.75f;
    
    // --- STATE AND INPUT VARIABLES ---
    private @PlayerInputs playerInputs;
    private CharacterController characterController;
    private Camera mainCamera; 

    // Public state: true when movement/look are enabled
    public bool IsControlsActive { get; private set; } = false; 
    
    private Vector2 moveInput;
    private Vector2 lookInput;
    private float rotationX = 0;

    void Awake()
    {
        // Singleton setup
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
        
        // Start unlocked/inactive, ready for activation input
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
        
        if (characterController == null)
        {
            Debug.LogError("CharacterController component missing on FirstPersonMovement GameObject.");
            enabled = false;
        }
    }
    
    // -------------------------------------------------------------
    // INPUT CALLBACKS (Used for Movement/Look/Cancel/Activate)
    // -------------------------------------------------------------
    
    public void OnMove(InputAction.CallbackContext context)
    {
        // Movement input is always read, but only applied if IsControlsActive is true
        moveInput = context.performed ? context.ReadValue<Vector2>() : Vector2.zero;
    }

    public void OnLook(InputAction.CallbackContext context)
    {
        // Look input is always read, but only applied if IsControlsActive is true
        lookInput = context.performed ? context.ReadValue<Vector2>() : Vector2.zero;
    }
    
    /// <summary>
    /// Locks the cursor and activates movement/look controls.
    /// </summary>
    public void OnActivate(InputAction.CallbackContext context)
    {
        if (context.performed && !IsControlsActive)
        {
            SetControlsActive(true);
        }
    }
    
    /// <summary>
    /// Unlocks the cursor and deactivates controls.
    /// </summary>
    public void OnCancel(InputAction.CallbackContext context)
    {
        if (context.performed && IsControlsActive)
        {
            SetControlsActive(false);
        }
    }
    
    // -------------------------------------------------------------
    // MOVEMENT AND ROTATION LOGIC (Update Loop)
    // -------------------------------------------------------------

    void Update()
    {
        HandleRotation();
        HandleMovement();
    }

    private void HandleRotation()
    {
        if (!IsControlsActive)
        {
            return;
        }

        transform.Rotate(0, lookInput.x * lookSpeed, 0);

        rotationX -= lookInput.y * lookSpeed;
        rotationX = Mathf.Clamp(rotationX, -90f, 90f); 
        
        if (mainCamera != null)
        {
             mainCamera.transform.localRotation = Quaternion.Euler(rotationX, 0, 0);
        }
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
             yStick = -0.1f; // Apply minimal gravity force
        }
        
        Vector3 finalMove = new Vector3(desiredMove.x, yStick, desiredMove.z);
        characterController.Move(finalMove * Time.deltaTime);
    }
    
    // -------------------------------------------------------------
    // CORE STATE MANAGEMENT
    // -------------------------------------------------------------

    /// <summary>
    /// Toggles control state and manages cursor lock/visibility.
    /// </summary>
    /// <param name="isActive">If true, locks cursor and enables movement.</param>
    public void SetControlsActive(bool isActive)
    {
        // Only proceed if the state is actually changing
        if (IsControlsActive == isActive)
        {
            return;
        }

        IsControlsActive = isActive; 

        if (isActive)
        {
            // Lock cursor for FPS control
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false; 
        }
        else
        {
            // Unlock cursor for UI interaction
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true; 
            
            // Stop residual movement when controls are deactivated
            moveInput = Vector2.zero;
            lookInput = Vector2.zero;
        }
    }
}