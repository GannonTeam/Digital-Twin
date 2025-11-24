using UnityEngine;
using System.Collections;

namespace Convai.Scripts.Runtime.Features.CustomActions 
{
    // All custom actions must implement this interface.
    public interface ICustomAction
    {
        // Name of the action, e.g., "Highlight"
        string ActionName { get; }

        // Reference to the action handler (used to access the NPC, services, etc.)
        void Initialize(ConvaiActionsHandler handler); 

        // The core method that executes the action logic.
        // Returns an IEnumerator to support time-based actions (like MoveTo, Animation).
        IEnumerator Execute(GameObject target); 
    }
}