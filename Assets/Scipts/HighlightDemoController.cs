using UnityEngine;
using System.Collections;
using System.Linq;

/// <summary>
/// Demo script to test HighlightingService using an automatic, time-based toggle loop
/// for the first 3 registered objects. This is ideal for debugging the HighlightingService itself.
/// </summary>
public class HighlightDemoController : MonoBehaviour
{
    private HighlightingService _service;
    private string[] _ids;
    private const float TOGGLE_INTERVAL = 2.0f; // Time in seconds to hold the highlight

    void Start()
    {
        _service = FindObjectOfType<HighlightingService>();
        if (_service == null)
        {
            Debug.LogError("[HighlightDemoController] HighlightingService not found in scene.");
            return;
        }

        // 1. Register all HighlightableObjects automatically
        _service.RegisterAllInScene();

        // 2. Build id list (first 3 found)
        _ids = _service.GetRegisteredIds().Take(3).ToArray();
        Debug.Log($"[HighlightDemoController] Found and will cycle through Ids: {string.Join(", ", _ids)}");

        if (_ids.Length > 0)
        {
            // 3. Start the automatic toggling routine
            StartCoroutine(HighlightCycleRoutine());
        }
        else
        {
            Debug.LogWarning("[HighlightDemoController] No highlightable IDs registered. Check if HighlightableObject components are in the scene.");
        }
    }

    /// <summary>
    /// Coroutine that cycles through the first three registered IDs, enabling and disabling the highlight.
    /// </summary>
    private IEnumerator HighlightCycleRoutine()
    {
        int index = 0;
        
        // Loop forever
        while (true)
        {
            if (_ids.Length == 0) yield break; // Safety break

            // Determine which object to toggle
            index = (index + 1) % _ids.Length;
            var idToToggle = _ids[index];

            // 1. Enable Highlight
            Debug.Log($"[HighlightDemoController] Enabling highlight for id '{idToToggle}' at {System.DateTime.Now:HH:mm:ss.fff}");
            _service.EnableById(idToToggle);

            // 2. Wait for the set interval
            yield return new WaitForSeconds(TOGGLE_INTERVAL);

            // 3. Disable Highlight
            Debug.Log($"[HighlightDemoController] Disabling highlight for id '{idToToggle}' at {System.DateTime.Now:HH:mm:ss.fff}");
            _service.DisableById(idToToggle);

            // 4. Wait again before starting the next object
            yield return new WaitForSeconds(TOGGLE_INTERVAL / 2f); 
        }
    }
}