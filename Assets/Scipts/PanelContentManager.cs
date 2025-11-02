using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Manages a collection of content panels. When a new index is received (typically from a Dropdown),
/// it hides all panels and shows only the panel corresponding to the index.
/// </summary>
public class PanelContentManager : MonoBehaviour
{
    [Tooltip("Drag all content panels (Log, Dashboard, etc.) into this list, in the order they appear in the Dropdown.")]
    public List<GameObject> contentPanels = new List<GameObject>();

    [Tooltip("Check this box to output console messages when the panel view changes.")]
    public bool debugLogChanges = false;

    private void Start()
    {
        // Ensure all panels are disabled at the start of the scene.
        // The ShowPanel() function will be called immediately by the Dropdown's default value (0)
        // once the scene loads and the UI is initialized.
        HideAllPanels();
        ShowPanel(0);
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
    /// Public function called by the Dropdown's "On Value Changed (Int)" event.
    /// </summary>
    /// <param name="panelIndex">The index of the selected item in the dropdown (0 = Log, 1 = Dashboard, etc.).</param>
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
            Debug.LogError($"Panel Manager Error: Panel at index {panelIndex} is null/missing in the list!");
        }
    }
}
