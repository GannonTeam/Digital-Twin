using UnityEngine;
using UnityEngine.InputSystem;

namespace Convai.Scripts.Runtime.Custom
{
    [RequireComponent(typeof(Collider))]
    public class ClickableDashboardToggle : MonoBehaviour
    {
        [Header("UI Reference")]
        [SerializeField] private GameObject dashboardUI;

        [Header("Input Action")]
        [SerializeField] private InputActionReference clickAction;

        [Header("Printer mapping")]
        [SerializeField] private string printerDevId = ""; 

        [Header("Behavior")]
        [SerializeField] private bool toggleWhenSame = false;

        private Camera _mainCamera;

        private void Awake()
        {
            _mainCamera = Camera.main;
            if (dashboardUI == null)
            {
                Debug.LogError($"ClickableDashboardToggle on {gameObject.name}: dashboardUI is not assigned.");
                enabled = false;
                return;
            }
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
                if (string.IsNullOrEmpty(printerDevId))
                {
                    OpenDashboard(null);
                    return;
                }

                // CHECK: Is the dashboard active and showing this printer?
                bool currentlyShowingThis = IsDashboardShowingPrinter(printerDevId); 

                if (toggleWhenSame && currentlyShowingThis)
                {
                    CloseDashboard();
                }
                else
                {
                    OpenDashboard(printerDevId);
                }
            }
        }

        public void OpenDashboard(string idToShow)
        {
            if (dashboardUI == null) return;

            if (!dashboardUI.activeSelf)
            {
                dashboardUI.SetActive(true);
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            var mgr = DashboardManager.Instance;
            if (mgr != null)
            {
                // Passing the devId and this GameObject's name as the fallback
                mgr.ShowPrinter(idToShow, this.gameObject.name);
            }
        }

        public void CloseDashboard()
        {
            if (dashboardUI == null) return;

            if (dashboardUI.activeSelf)
            {
                dashboardUI.SetActive(false);
                // Return cursor control if needed
            }

            // FIX: Instead of ClearActivePanel(), we call ShowPrinter with empty values
            DashboardManager.Instance?.ShowPrinter(string.Empty, null);
        }

        private bool IsDashboardShowingPrinter(string devId)
        {
            // If UI is off, it's not showing anything
            if (!dashboardUI.activeSelf) return false;

            var mgr = DashboardManager.Instance;
            if (mgr == null || mgr.activePanel == null) return false;

            // Check the activePanel's CurrentPrinterId directly
            return mgr.activePanel.CurrentPrinterId == devId;
        }

        public string PrinterDevId
        {
            get => printerDevId;
            set => printerDevId = value;
        }
    }
}