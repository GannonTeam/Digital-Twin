using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using System.Collections;
using System;
using TMPro; // Required for TMP_Dropdown

/// <summary>
/// Handles the visibility of the main UI dashboard panel (uiPanel) when the linked 
/// object (e.g., a 3D printer) is clicked, and provides explicit Open/Close methods.
/// </summary>
public class ViewportController : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("The root GameObject of the entire dashboard panel to be shown/hidden.")]
    [SerializeField]
    private GameObject uiPanel; 

    [Tooltip("The PanelContentManager instance on the dashboard root.")]
    [SerializeField]
    private PanelContentManager contentManager;
    
    [Tooltip("The Dropdown component that displays the current active panel.")]
    [SerializeField]
    private TMP_Dropdown panelDropdown;
    
    // Default Panel settings
    [Header("Default View Configuration")]
    [Tooltip("The exact GameObject name of the Dashboard Panel (default view when clicking printer).")]
    [SerializeField]
    private string dashboardPanelName = "DashboardContentPanel";
    
    [Tooltip("The index in the Dropdown that corresponds to the Dashboard Panel (e.g., 1).")]
    [SerializeField]
    private int dashboardPanelIndex = 1;
    
    private Canvas rootCanvas;

    [Header("Input")]
    [Tooltip("The input action used to detect clicks on the linked object.")]
    [SerializeField]
    private InputActionReference clickAction; 
    
    // --- Private State ---
    private Camera mainCamera;

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
    }

    private void Start()
    {
        if (Camera.main != null)
        {
            mainCamera = Camera.main.GetComponent<Camera>();
        }

        if (mainCamera == null)
        {
            Debug.LogError("ViewportController Error: Main Camera not found or missing Camera component.");
            enabled = false;
        }
        
        rootCanvas = uiPanel?.GetComponentInParent<Canvas>();
        if (rootCanvas == null)
        {
            Debug.LogError("ViewportController Error: Could not find root Canvas for UI Panel.");
        }

        ClosePanel(true); 

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
                // Clicking the printer model always opens the default Dashboard view
                OpenPanelToDashboard();
            }
        }
    }

    /// <summary>
    /// Opens the main panel and sets the view to the Dashboard by default.
    /// This is called when the 3D printer model is clicked.
    /// </summary>
    public void OpenPanelToDashboard()
    {
        if (uiPanel != null)
        {
            uiPanel.SetActive(true);

            // Force layout update aggressively
            if (rootCanvas != null)
            {
                Canvas.ForceUpdateCanvases();
            }

            RectTransform panelRect = uiPanel.GetComponent<RectTransform>();
            if (panelRect != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);
            }
            
            // 1. Set dropdown value to Dashboard index
            if (panelDropdown != null)
            {
                panelDropdown.value = dashboardPanelIndex;
            }

            // 2. Switch content to Dashboard
            if (contentManager != null)
            {
                contentManager.ShowPanelByName(dashboardPanelName);
            }

            Debug.Log("ViewportController: Panel opened and set to Dashboard.");
        }
        else
        {
            Debug.LogError("ViewportController Error: uiPanel reference is missing!");
        }
    }
    
    /// <summary>
    /// Public function to explicitly open the view and show the panel.
    /// Used for delayed switching by the OpenChatButtonHandler.
    /// </summary>
    public void OpenPanel()
    {
        if (uiPanel != null)
        {
            // Simply activate the panel without changing content, as content is handled
            // by the ChatButtonHandler's delayed switch logic.
            uiPanel.SetActive(true);
            
            // Force layout update aggressively (still necessary for the delay logic to work)
            if (rootCanvas != null)
            {
                Canvas.ForceUpdateCanvases();
            }

            RectTransform panelRect = uiPanel.GetComponent<RectTransform>();
            if (panelRect != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);
            }

            Debug.Log("ViewportController: Panel activated (content switch pending).");
        }
        else
        {
            Debug.LogError("ViewportController Error: uiPanel reference is missing!");
        }
    }

    /// <summary>
    /// Public function that initiates a delayed opening and content switch.
    /// This is the method the OpenChatButtonHandler should use.
    /// </summary>
    /// <param name="callback">Action to execute one frame after the panel is opened.</param>
    public void StartDelayedContentSwitch(Action callback)
    {
        // 1. Ensure the panel is open (calling OpenPanel performs the layout rebuilds)
        OpenPanel();
        
        // 2. Start the coroutine to perform the content switch one frame later
        StartCoroutine(PerformDelayedAction(callback));
    }

    /// <summary>
    /// Coroutine that yields one frame before executing the provided action.
    /// </summary>
    private IEnumerator PerformDelayedAction(Action action)
    {
        yield return null; 
        action?.Invoke();
    }
    
    /// <summary>
    /// Public function to explicitly close the view and hide the panel.
    /// </summary>
    public void ClosePanel(bool forceClose = false)
    {
        if (uiPanel != null && (uiPanel.activeSelf || forceClose))
        {
            uiPanel.SetActive(false);
            if (!forceClose) 
            {
                Debug.Log("ViewportController: Panel closed.");
            }
        }
    }
}
