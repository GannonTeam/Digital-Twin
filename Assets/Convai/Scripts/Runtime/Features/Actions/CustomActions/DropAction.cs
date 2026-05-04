using System.Collections;
using Convai.Scripts.Runtime.LoggerSystem;
using Convai.Scripts.Runtime.Features;
using Convai.Scripts.Runtime.Features.CustomActions;
using UnityEngine;

// --- CORRECTED NAMESPACE ---
namespace Convai.Scripts.Runtime.Features.Actions.CustomActions
{
    public class DropAction : ICustomAction
    {
        public string ActionName => "Drop";

        private ConvaiActionsHandler _handler;

        public void Initialize(ConvaiActionsHandler handler)
        {
            _handler = handler;
        }

        public IEnumerator Execute(GameObject target)
        {
            _handler.SignalActionStarted(ActionName, target);

            // --- Original Drop Logic Restored ---
            if (target != null)
            {
                ConvaiLogger.DebugLog($"Dropping Target: {target.name}", ConvaiLogger.LogCategory.Actions);
                target.transform.parent = null;
                target.SetActive(true);
            }
            // --- End of Original Logic ---
            
            yield return null; 

            _handler.SignalActionEnded(ActionName, target);
        }
    }
}