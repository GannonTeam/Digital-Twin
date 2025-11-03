using UnityEngine;
using TMPro; // Required for using the TMP_Dropdown component

/// <summary>
/// Handles the action for a dedicated 'Open Chat' button. 
/// 1. Ensures the main dashboard panel is open (via ViewportController).
/// 2. Switches the internal content view to the Chat Panel (via PanelContentManager).
/// 3. Updates the associated Dropdown UI to the Chat index.
/// 
/// This script uses the ViewportController's StartDelayedContentSwitch method to introduce a one-frame 
/// delay between panel activation and content switching, fixing the first-click timing issue.
/// </summary>
public class OpenChatButtonHandler : MonoBehaviour
{
    [Tooltip("Drag the ViewportController script instance here (e.g., the one on your 3D printer object).")]
    [SerializeField]
    private ViewportController dashboardVisibilityController;

    [Tooltip("Drag the PanelContentManager script instance here (e.g., the one on the main dashboard panel).")]
    [SerializeField]
    private PanelContentManager contentManager;
    
    // --- NEW DROPDOWN CONTROL FIELDS ---
    [Header("Dropdown State Control")]
    [Tooltip("The Dropdown component that displays the current active panel (e.g., Chat or Dashboard).")]
    [SerializeField]
    private TMP_Dropdown panelDropdown;
    
    [Tooltip("The index in the Dropdown that corresponds to the Chat Panel (usually 0).")]
    [SerializeField]
    private int chatPanelIndex = 0;
    // ------------------------------------

    [Header("Content Name")]
    [Tooltip("The exact GameObject name of the Chat Panel. Must match the name used in PanelContentManager.")]
    [SerializeField]
    private string chatPanelName = "ChatPanel"; // **Ensure this matches your panel's name**

    /// <summary>
    /// Public function to be called by the dedicated UI Button's OnClick() event.
    /// </summary>
    public void OpenChatView()
    {
        if (dashboardVisibilityController == null)
        {
            Debug.LogError("OpenChatButtonHandler Error: Dashboard Visibility Controller reference is missing. Cannot open panel.");
            return;
        }

        if (contentManager == null)
        {
            Debug.LogError("OpenChatButtonHandler Error: Content Manager reference is missing. Cannot switch content.");
            return;
        }

        // We use StartDelayedContentSwitch. This method first calls OpenPanel() immediately, 
        // then yields for one frame before executing the content switch logic provided in the callback.
        dashboardVisibilityController.StartDelayedContentSwitch(() =>
        {
            // 1. Update the dropdown value to reflect the Chat panel is now active
            if (panelDropdown != null && panelDropdown.value != chatPanelIndex)
            {
                panelDropdown.value = chatPanelIndex;
            }
            else if (panelDropdown == null)
            {
                Debug.LogWarning("OpenChatButtonHandler: Panel Dropdown reference is missing. Cannot sync UI state, but proceeding with content switch.");
            }

            // 2. Switch the actual content panel
            contentManager.ShowPanelByName(chatPanelName);
            Debug.Log($"OpenChatButtonHandler: Successfully requested content switch to '{chatPanelName}'.");
        });
    }
}
