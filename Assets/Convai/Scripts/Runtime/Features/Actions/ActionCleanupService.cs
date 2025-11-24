using UnityEngine;
using Convai.Scripts.Runtime.Features;
using System.Collections;
using Convai.Scripts.Runtime.Core; // Necessary for ConvaiNPC reference and Coroutine

public class ActionCleanupService : MonoBehaviour
{
    // Reference to the services needed for cleanup
    private HighlightingService _highlightingService;
    private ConvaiActionsHandler _actionsHandler;
    private ConvaiNPC _npc; // Reference to the NPC to subscribe to its conversation end event

    // State management
    private GameObject _currentlyHighlightedTarget = null;
    private const string SHOW_ME_ACTION_NAME = "Show Me";
    
    // --- ASSUMPTION: Replace this with your actual Convai communication component ---
    // private ConvaiChatScript _chatScript; 

    void Start()
    {
        // 1. Get service references
        _highlightingService = FindObjectOfType<HighlightingService>();
        _actionsHandler = GetComponent<ConvaiActionsHandler>();
        
        // --- ADDED: Get the NPC reference on this same GameObject ---
        _npc = GetComponent<ConvaiNPC>();

        if (_actionsHandler == null || _highlightingService == null || _npc == null)
        {
            Debug.LogError("ActionCleanupService requires ConvaiNPC, ConvaiActionsHandler, and HighlightingService components in the scene.");
            return;
        }

        // 2. Subscribe to action events to track the target object
        _actionsHandler.ActionStarted += OnActionStarted;
        _actionsHandler.ActionEnded += OnActionEnded;

        // 3. Subscribe to the NPC conversation end event (This is the critical cleanup hook)
        _npc.OnNPCTurnFinished += HandleNPCTurnFinished; 
        
        // --- MODIFIED: Log subscription success after all dependencies are found ---
        Debug.Log("CLEANUP: Subscribed to NPC Turn Finished event.");
    }

    void OnDestroy()
    {
        if (_actionsHandler != null)
        {
            _actionsHandler.ActionStarted -= OnActionStarted;
            _actionsHandler.ActionEnded -= OnActionEnded;
        }
        // --- ADDED: Unsubscribe from the NPC event ---
        if (_npc != null)
        {
             _npc.OnNPCTurnFinished -= HandleNPCTurnFinished;
        }
    }

    private void OnActionStarted(string actionName, GameObject target)
    {
        // If the 'Show Me' action starts, record the target.
        if (actionName.Equals(SHOW_ME_ACTION_NAME, System.StringComparison.OrdinalIgnoreCase))
        {
            _currentlyHighlightedTarget = target;
            Debug.Log($"CLEANUP STATE: Action '{actionName}' started. Target recorded as {target?.name}.");
        }
    }

    private void OnActionEnded(string actionName, GameObject target)
    {
        // After the move/highlight sequence is complete, ensure the highlight is enabled 
        // while the NPC starts speaking.
        if (actionName.Equals(SHOW_ME_ACTION_NAME, System.StringComparison.OrdinalIgnoreCase))
        {
             // Wait one frame before forcing the highlight to ensure the action coroutine finishes cleanup
             StartCoroutine(EnableFinalHighlight(target));
        }
    }

    IEnumerator EnableFinalHighlight(GameObject target)
    {
        // Wait one frame to ensure all coroutines from ShowMeAction are finished
        yield return null; 
        if (_currentlyHighlightedTarget != null)
        {
            _highlightingService.EnableHighlight(_currentlyHighlightedTarget, Color.yellow);
        }
    }


    /// <summary>
    /// Handler triggered when the NPC finishes speaking (via OnNPCTurnFinished).
    /// This is where the cleanup process begins.
    /// </summary>
    private void HandleNPCTurnFinished(ConvaiNPC finishedNPC)
    {
        // Check if the target was set by the Show Me action
        if (_currentlyHighlightedTarget != null)
        {
            // --- ADDED LOG 2 ---
            Debug.Log($"CLEANUP: Starting delayed cleanup for {_currentlyHighlightedTarget.name}.");
            StartCoroutine(DelayedUnhighlight(2.0f)); 
        }
        else
        {
            // --- ADDED LOG 3 ---
            Debug.Log("CLEANUP: Received turn finished, but _currentlyHighlightedTarget is NULL. No cleanup needed.");
        }
    }
    
    private IEnumerator DelayedUnhighlight(float delay)
    {
        Debug.Log($"CLEANUP COROUTINE: Starting {delay}s delay.");
        yield return new WaitForSeconds(delay);

        if (_currentlyHighlightedTarget != null && _highlightingService != null)
        {
            _highlightingService.DisableHighlight(_currentlyHighlightedTarget);
            Debug.Log($"CLEANUP SUCCESS: Disabled highlight for {_currentlyHighlightedTarget.name}.");
        }
        else
        {
            // --- ADDED LOG 6 (Error) ---
            Debug.LogError($"CLEANUP FAILURE: Cannot unhighlight. Target was null ({_currentlyHighlightedTarget == null}) or Service was null ({_highlightingService == null}).");
        }
    
        _currentlyHighlightedTarget = null; // Reset state
    }
}