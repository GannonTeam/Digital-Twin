using UnityEngine;
using UnityEngine.InputSystem;

namespace Convai.Scripts.Runtime.Custom
{
    /// <summary>
    /// Handles interaction via the New Input System. 
    /// To be placed on the clickable object. Opens the assigned dashboard UI on click 
    /// and provides a public method to close it.
    /// </summary>
    [RequireComponent(typeof(Collider))] // Still requires a collider for the hit check
    public class ClickableDashboardToggle : MonoBehaviour
    {
        [Header("UI Reference")]
        [Tooltip("The Canvas or GameObject containing the dashboard UI to be opened/closed.")]
        [SerializeField] private GameObject dashboardUI;

        [Header("Input Action")]
        [Tooltip("CRITICAL: Drag the specific 'Click' or 'Attack' Action asset reference here (e.g., Player/Attack).")]
        [SerializeField] private InputActionReference clickAction;

        private Camera _mainCamera;

        private void Awake()
        {
            _mainCamera = Camera.main;

            if (_mainCamera == null)
            {
                Debug.LogError("ClickableDashboardToggle requires a main camera in the scene to determine hit position.");
                enabled = false;
            }

            if (dashboardUI == null)
            {
                Debug.LogError($"ClickableDashboardToggle on {gameObject.name}: Dashboard UI GameObject is not assigned.");
                enabled = false;
            }
            else
            {
                // Ensure the dashboard starts hidden
                dashboardUI.SetActive(false);
            }
        }

        private void OnEnable()
        {
            // Subscribe to the Input Action events.
            if (clickAction != null && clickAction.action != null)
            {
                clickAction.action.performed += HandleClickPerformed;
                clickAction.action.Enable();
            }
        }

        private void OnDisable()
        {
            // Unsubscribe and disable the Input Action.
            if (clickAction != null && clickAction.action != null)
            {
                clickAction.action.performed -= HandleClickPerformed;
                clickAction.action.Disable();
            }
        }

        /// <summary>
        /// Called when the player performs the assigned click action (LMB).
        /// </summary>
        private void HandleClickPerformed(InputAction.CallbackContext context)
        {
            if (_mainCamera == null) return;
            
            Ray ray = _mainCamera.ScreenPointToRay(Mouse.current.position.value);
            RaycastHit hit;

            // Perform raycast and check if the hit object is this one.
            if (Physics.Raycast(ray, out hit) && hit.collider == GetComponent<Collider>())
            {
                // Only OPEN the dashboard on click, never close it here.
                Debug.Log($"Opening Dashboard via click on {gameObject.name}");
                OpenDashboard();
            }
        }

        /// <summary>
        /// Explicitly opens the dashboard UI.
        /// </summary>
        public void OpenDashboard()
        {
            if (dashboardUI != null && !dashboardUI.activeSelf)
            {
                dashboardUI.SetActive(true);
                // Ensure the cursor is available for UI interaction when the dashboard is open.
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        /// <summary>
        /// Public method to explicitly close the dashboard UI. This should be called by a button.
        /// </summary>
        public void CloseDashboard()
        {
            if (dashboardUI != null && dashboardUI.activeSelf)
            {
                dashboardUI.SetActive(false);
                // Allow the player movement system (ConvaiPlayerMovement) to handle the cursor state for movement.
            }
        }
    }
}