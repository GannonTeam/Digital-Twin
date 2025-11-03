using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Manages a collection of content panels. When a new index or name is received,
/// it hides all panels and shows only the panel corresponding to the request.
/// </summary>
public class PanelContentManager : MonoBehaviour
{
    [Tooltip("Drag all content panels (e.g., ChatPanel, DashboardContentPanel) into this list. The order corresponds to Dropdown indices.")]
    public List<GameObject> contentPanels = new List<GameObject>();

    [Tooltip("Check this box to output console messages when the panel view changes.")]
    public bool debugLogChanges = false;

    private void Start()
    {
        // Ensure all panels are disabled at the start of the scene.
        HideAllPanels();
        
        // Show the default panel (Index 0).
        if (contentPanels.Count > 0)
        {
            ShowPanel(0);
        }
        else
        {
            Debug.LogWarning("PanelContentManager: contentPanels list is empty. No panels to show on Start.");
        }
    }

    /// <summary>
    /// Hides all GameObjects in the contentPanels list.
    /// </summary>
    private void HideAllPanels()
    {
        foreach (GameObject panel in contentPanels)
        {
            if (panel != null)
            {
                panel.SetActive(false);
            }
        }
    }

    /// <summary>
    /// Public function called by a Dropdown's "On Value Changed (Int)" event.
    /// Switches content panel based on its index in the list.
    /// </summary>
    /// <param name="panelIndex">The index of the selected item (e.g., 0 for ChatPanel, 1 for DashboardContentPanel).</param>
    public void ShowPanel(int panelIndex)
    {
        // 1. Check for valid index before proceeding.
        if (panelIndex < 0 || panelIndex >= contentPanels.Count)
        {
            Debug.LogError($"Panel Manager Error: Received invalid index {panelIndex}. Index must be between 0 and {contentPanels.Count - 1}.");
            return;
        }

        // 2. Hide whatever was previously active.
        HideAllPanels();

        // 3. Activate the requested panel.
        GameObject panelToShow = contentPanels[panelIndex];
        
        if (panelToShow != null)
        {
            panelToShow.SetActive(true);

            if (debugLogChanges)
            {
                Debug.Log($"Panel Manager: Switched view to index {panelIndex} ({panelToShow.name}).");
            }
        }
        else
        {
            Debug.LogError($"Panel Manager Error: Panel at index {panelIndex} is null/missing in the list! Check the contentPanels assignment.");
        }
    }
    
    /// <summary>
    /// Public function called by a UI Button or script logic to switch content by panel name.
    /// This is the required method for the new 'Open Chat' button functionality.
    /// </summary>
    /// <param name="panelName">The exact name of the GameObject panel to show (e.g., "ChatPanel").</param>
    public void ShowPanelByName(string panelName)
    {
        // Finds the index of the GameObject with the matching name.
        int panelIndex = contentPanels.FindIndex(panel => panel != null && panel.name == panelName);

        if (panelIndex != -1)
        {
            // Use the index to call the core panel switching logic.
            ShowPanel(panelIndex);
        }
        else
        {
            Debug.LogError($"Panel Manager Error: Panel named '{panelName}' not found in the contentPanels list. Check spelling and list contents.");
        }
    }
}
