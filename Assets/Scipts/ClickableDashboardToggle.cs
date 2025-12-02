using UnityEngine;
using UnityEngine.InputSystem;
using Convai.Scripts.Runtime.Core; // Added if necessary for global components

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
        [Tooltip("Must match the backend PrinterId for this machine.")]
        [SerializeField] private string printerId = "";

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
                // If no printer id configured, warn and open UI but do not call manager
                if (string.IsNullOrEmpty(printerId))
                {
                    Debug.LogWarning($"ClickableDashboardToggle on {gameObject.name} has no PrinterId configured. Opening dashboard without selecting a printer.");
                    OpenDashboard(null);
                    return;
                }

                // If toggle is enabled and dashboard already showing this printer, close it
                var mgr = DashboardManager.Instance;
                bool currentlyShowingThis = mgr != null && IsDashboardShowingPrinter(printerId);

                if (toggleWhenSame && currentlyShowingThis)
                {
                    CloseDashboard();
                }
                else
                {
                    OpenDashboard(printerId);
                }
            }
        }

        /// <summary>
        /// Open the dashboard UI and request DashboardManager to show the given printer (if not null).
        /// </summary>
        public void OpenDashboard(string idToShow)
        {
            Debug.Log($"[DASHBOARD DEBUG] OpenDashboard called for ID: {idToShow}."); // <-- LOG 1: Method entered

            if (dashboardUI == null)
            {
                Debug.LogError("[DASHBOARD DEBUG] ERROR: dashboardUI reference is NULL in the Inspector. Cannot open."); // <-- LOG 2: CRITICAL FAILURE CHECK
                return;
            }

            if (!dashboardUI.activeSelf)
            {
                Debug.Log("[DASHBOARD DEBUG] Activating dashboardUI now."); // <-- LOG 3: Activation SUCCESS signal
                dashboardUI.SetActive(true);
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Debug.Log("[DASHBOARD DEBUG] dashboardUI is ALREADY ACTIVE. Proceeding to bind."); // <-- LOG 4: Already open
            }

            if (!string.IsNullOrEmpty(idToShow))
            {
                var mgr = DashboardManager.Instance;
                if (mgr == null)
                {
                    Debug.LogError("ClickableDashboardToggle: DashboardManager.Instance is null. Ensure DashboardManager exists in the scene.");
                    return;
                }

                // Ask DashboardManager to show the printer; DashboardManager handles caching and requesting data.
                mgr.ShowPrinter(idToShow);

                // Optionally trigger a per-id request in the polling client for fastest initial fill:
                if (requestSingleOnShow)
                {
                    var poller = FindObjectOfType<PrinterPollingClient>();
                    if (poller != null)
                    {
                        // Assuming PrinterPollingClient.RequestSingle exists
                        // poller.RequestSingle(idToShow);
                    }
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

        private bool IsDashboardShowingPrinter(string id)
        {
            // Since activePrinterId is private inside DashboardManager, we detect by comparing cached state or latest displayed:
            // Try to read cached state and see if the active panel is bound to that id:
            // We can check the activePanel property if you expose it, but to keep coupling minimal:
            // We'll compare the cached latest state for the id and whether dashboard UI is active.
            if (!dashboardUI.activeSelf) return false;

            if (DashboardManager.Instance == null) return false;

            if (DashboardManager.Instance.TryGetLatestState(id, out var cached))
            {
                // If there's a cached state for this id, assume it's what would be shown.
                // This is conservative; DashboardManager.ShowPrinter is the single source of truth.
                return true;
            }

            // If no cached state, assume it's not currently showing
            return false;
        }

        /// <summary>
        /// Expose PrinterId for inspector or runtime changes.
        /// </summary>
        public string PrinterId
        {
            get => printerId;
            set => printerId = value;
        }
    }
}