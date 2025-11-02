using UnityEngine;
using UnityEngine.InputSystem; 

// FIX: Changed IPlayerActions to IPlayerMovementActions
public class FirstPersonMovement : MonoBehaviour, @PlayerInputs.IPlayerMovementActions 
{
    // --- ADJUSTABLE SETTINGS ---
    [Header("Movement Settings")]
    public float walkingSpeed = 4.0f;
    
    [Header("Camera Settings")]
    public float lookSpeed = 0.75f;

    // --- STATE AND INPUT VARIABLES ---
    private @PlayerInputs playerInputs;
    private CharacterController characterController;
    
    private bool isControlsActive = false; // Tracks if movement/look is allowed
    
    private Vector2 moveInput;
    private Vector2 lookInput;
    private float rotationX = 0;

    void Awake()
    {
        // Instantiate the Input Action Class
        playerInputs = new @PlayerInputs();
    }

    void OnEnable()
    {
        // FIX: Changed playerInputs.Player to playerInputs.PlayerMovement
        playerInputs.PlayerMovement.SetCallbacks(this);
        playerInputs.PlayerMovement.Enable(); // FIX: Changed playerInputs.Player to playerInputs.PlayerMovement
        
        // Ensure the OS cursor is always visible and unlocked on start
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Start in the deactivated state
        SetControlsActive(false); 
    }

    void OnDisable()
    {
        playerInputs.PlayerMovement.Disable(); // FIX: Changed playerInputs.Player to playerInputs.PlayerMovement
        playerInputs.PlayerMovement.RemoveCallbacks(this); // FIX: Changed playerInputs.Player to playerInputs.PlayerMovement
    }

    void Start()
    {
        characterController = GetComponent<CharacterController>();
    }
    
    // -------------------------------------------------------------
    // INPUT CALLBACKS (IPlayerMovementActions Implementation)
    // -------------------------------------------------------------
    
    // Reads WASD input
    public void OnMove(InputAction.CallbackContext context)
    {
        // Read the value if the action is performed, otherwise set to zero.
        moveInput = context.performed ? context.ReadValue<Vector2>() : Vector2.zero;
    }

    // Reads Mouse Delta input (only active when RMB is held, due to asset binding)
    public void OnLook(InputAction.CallbackContext context)
    {
        lookInput = context.performed ? context.ReadValue<Vector2>() : Vector2.zero;
    }
    
    // Activate: Left Mouse Button Down (Enter Control Mode)
    public void OnActivate(InputAction.CallbackContext context)
    {
        // If Left Click is performed AND we are NOT in control mode, activate.
        if (context.performed && !isControlsActive)
        {
            SetControlsActive(true);
        }
    }
    
    // Cancel: Escape Key Down (Exit Control Mode)
    public void OnCancel(InputAction.CallbackContext context)
    {
        // If Escape is pressed AND we are currently in control mode, deactivate.
        if (context.performed && isControlsActive)
        {
            SetControlsActive(false);
        }
    }
    
    // -------------------------------------------------------------
    // STATE MANAGEMENT
    // -------------------------------------------------------------

    private void SetControlsActive(bool isActive)
    {
        isControlsActive = isActive;

        // The OS cursor remains visible and unlocked regardless of state.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true; 

        if (isActive)
        {
            // Optionally, hide the OS cursor when active if you want a cleaner look while moving
            // Cursor.visible = false;
            Debug.Log("Controls Activated (Ready for RMB Look)");
        }
        else
        {
            // Stop movement immediately upon deactivating controls
            moveInput = Vector2.zero;
            lookInput = Vector2.zero;
            Debug.Log("Controls Deactivated (Use Left Click to Re-Activate)");
        }
    }

    // -------------------------------------------------------------
    // MOVEMENT AND ROTATION LOGIC
    // -------------------------------------------------------------

    void Update()
    {
        HandleRotation();
        HandleMovement();
    }

    private void HandleRotation()
    {
        // Only allow looking if controls are active (Left Click was pressed).
        if (!isControlsActive)
        {
            return;
        }

        // Horizontal Rotation (Yaw)
        transform.Rotate(0, lookInput.x * lookSpeed, 0);

        // Vertical Rotation (Pitch)
        rotationX -= lookInput.y * lookSpeed;
        rotationX = Mathf.Clamp(rotationX, -90f, 90f); 
        Camera.main.transform.localRotation = Quaternion.Euler(rotationX, 0, 0);
    }

    private void HandleMovement()
    {
        // Stop movement immediately if controls are not active.
        if (!isControlsActive)
        {
            return;
        }

        Vector3 forwardMovement = transform.forward * moveInput.y;
        Vector3 rightMovement = transform.right * moveInput.x;
        Vector3 desiredMove = (forwardMovement + rightMovement).normalized * walkingSpeed;
        
        // --- Stick to the ground logic (No Gravity/Falling) ---
        float yStick = 0;
        if (characterController.isGrounded)
        {
             // Apply a small downward force to keep it stuck to the floor/slopes.
             yStick = -0.1f; 
        }
        
        Vector3 finalMove = new Vector3(desiredMove.x, yStick, desiredMove.z);
        characterController.Move(finalMove * Time.deltaTime);
    }
}
