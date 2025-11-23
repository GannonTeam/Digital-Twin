using UnityEngine;

/// <summary>
/// Marker component for objects that can be highlighted by the HighlightingService.
/// Carries an elementId for AI/system targeting.
/// </summary>
public class HighlightableObject : MonoBehaviour
{
    [Tooltip("Unique id for this highlightable object. Use this id when calling the HighlightingService from AI.")]
    public string elementId;

    // The defaultOutlineColor field has been removed.
}