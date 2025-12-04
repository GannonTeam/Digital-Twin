using UnityEngine;
using UnityEngine.InputSystem;
// You may keep or remove the Convai using statement based on your project's needs.
// using Convai.Scripts.Runtime.Core; 

namespace Convai.Scripts.Runtime.Custom
{
    /// <summary>
    /// Handles interaction via the New Input System.
    /// Should be placed on each clickable machine object.
    /// Opens a single shared dashboard UI and tells DashboardManager which printer to show.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class ClickableDashboardToggle : MonoBehaviour
    {
        [Header("UI Reference")]
        [Tooltip("The Canvas or GameObject containing the single dashboard UI panel.")]
        [SerializeField] private GameObject dashboardUI;

        [Header("Input Action")]
        [Tooltip("Drag the specific 'Click' InputActionReference here.")]
        [SerializeField] private InputActionReference clickAction;

        [Header("Printer mapping")]
        [Tooltip("Must match the backend devId for this machine.")]
        [SerializeField] private string printerDevId = ""; // MODIFIED: Renamed field to align with backend JSON

        [Header("Behavior")]
        [Tooltip("If true, clicking the same machine when dashboard is open will close it (toggle).")]
        [SerializeField] private bool toggleWhenSame = false;

        [Tooltip("If true, when showing a printer the polling client will request single-printer endpoint (if supported).")]
        [SerializeField] private bool requestSingleOnShow = true;

        private Camera _mainCamera;

        private void Awake()
        {
            _mainCamera = Camera.main;

            if (_mainCamera == null)
            {
                Debug.LogError("ClickableDashboardToggle requires a main camera in the scene.");
                enabled = false;
                return;
            }

            if (dashboardUI == null)
            {
                Debug.LogError($"ClickableDashboardToggle on {gameObject.name}: dashboardUI is not assigned.");
                enabled = false;
                return;
            }

            // Start closed by default
            dashboardUI.SetActive(false);
        }

        private void OnEnable()
        {
            if (clickAction != null && clickAction.action != null)
            {
                clickAction.action.performed += HandleClickPerformed;
                clickAction.action.Enable();
            }
        }

        private void OnDisable()
        {
            if (clickAction != null && clickAction.action != null)
            {
                clickAction.action.performed -= HandleClickPerformed;
                clickAction.action.Disable();
            }
        }

        private void HandleClickPerformed(InputAction.CallbackContext context)
        {
            if (_mainCamera == null) return;

            var mousePos = Mouse.current.position.ReadValue();
            var ray = _mainCamera.ScreenPointToRay(mousePos);
            if (Physics.Raycast(ray, out var hit) && hit.collider == GetComponent<Collider>())
            {
                // Check against the new devId field
                if (string.IsNullOrEmpty(printerDevId))
                {
                    Debug.LogWarning($"ClickableDashboardToggle on {gameObject.name} has no PrinterDevId configured. Opening dashboard without selecting a printer.");
                    OpenDashboard(null);
                    return;
                }

                // If toggle is enabled and dashboard already showing this printer, close it
                var mgr = DashboardManager.Instance;
                // Use the new devId field
                bool currentlyShowingThis = mgr != null && IsDashboardShowingPrinter(printerDevId); 

                if (toggleWhenSame && currentlyShowingThis)
                {
                    CloseDashboard();
                }
                else
                {
                    // Pass the new devId field
                    OpenDashboard(printerDevId);
                }
            }
        }

        /// <summary>
        /// Open the dashboard UI and request DashboardManager to show the given printer (if not null).
        /// </summary>
        public void OpenDashboard(string idToShow)
        {
            Debug.Log($"[DASHBOARD DEBUG] OpenDashboard called for ID: {idToShow}."); 

            if (dashboardUI == null)
            {
                Debug.LogError("[DASHBOARD DEBUG] ERROR: dashboardUI reference is NULL in the Inspector. Cannot open.");
                return;
            }

            // --- UI Activation Logic ---
            if (!dashboardUI.activeSelf)
            {
                Debug.Log("[DASHBOARD DEBUG] Activating dashboardUI now.");
                dashboardUI.SetActive(true);
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Debug.Log("[DASHBOARD DEBUG] dashboardUI is ALREADY ACTIVE. Proceeding to bind/clear.");
            }
            // --------------------------

            var mgr = DashboardManager.Instance;
            if (mgr == null)
            {
                Debug.LogError("ClickableDashboardToggle: DashboardManager.Instance is null. Ensure DashboardManager exists in the scene.");
                return;
            }

            // FIX: ALWAYS call ShowPrinter() to bind/clear, even if idToShow is null/empty.
            // If idToShow is null/empty, ShowPrinter will tell the UI Handler to clear.
            mgr.ShowPrinter(idToShow, this.gameObject.name);

            // ACTIVATED: Use RequestSingle for the fastest initial data fill (only if we have an ID).
            if (!string.IsNullOrEmpty(idToShow) && requestSingleOnShow)
            {
                var poller = FindObjectOfType<PrinterPollingClient>();
                if (poller != null)
                {
                    poller.RequestSingle(idToShow); 
                }
            }
        }

        /// <summary>
        /// Closes the dashboard UI (does not clear cache).
        /// </summary>
        public void CloseDashboard()
        {
            if (dashboardUI == null) return;

            if (dashboardUI.activeSelf)
            {
                dashboardUI.SetActive(false);
                // Optionally return control of the cursor to your player movement system here.
                // For example: ConvaiPlayerMovement.Instance?.RestoreCursorState();
            }

            // Optionally unbind the active panel if you want the UI cleared whenever closed:
            // DashboardManager.Instance?.ClearActivePanel();
        }

        private bool IsDashboardShowingPrinter(string devId) // Parameter name changed for clarity
        {
            if (!dashboardUI.activeSelf) return false;

            if (DashboardManager.Instance == null) return false;

            // Use the TryGetLatestState method which uses devId as the key
            if (DashboardManager.Instance.TryGetLatestState(devId, out var cached))
            {
                // If there's a cached state for this id, assume it's what would be shown.
                return true;
            }

            // If no cached state, assume it's not currently showing
            return false;
        }

        /// <summary>
        /// Expose PrinterDevId for inspector or runtime changes.
        /// </summary>
        public string PrinterDevId // Property name changed
        {
            get => printerDevId;
            set => printerDevId = value;
        }
    }
}