using UnityEngine;
using System.Collections;
using Convai.Scripts.Runtime.Features.CustomActions;
using Convai.Scripts.Runtime.LoggerSystem;

namespace Convai.Scripts.Runtime.Features.Actions.CustomActions 
{
    public class HighlightAction : ICustomAction
    {
        public string ActionName => "Highlight"; 

        private ConvaiActionsHandler _handler;
        private HighlightingService _highlightingService; 

        public void Initialize(ConvaiActionsHandler handler)
        {
            _handler = handler;
            _highlightingService = Object.FindObjectOfType<HighlightingService>();
        }

        public IEnumerator Execute(GameObject target)
        {
            // --- MODIFIED: Use the safe signal method on the handler ---
            _handler.SignalActionStarted(ActionName, target); 

            if (_highlightingService != null && target != null)
            {
                ConvaiLogger.DebugLog($"Executing {ActionName} on {target.name}", ConvaiLogger.LogCategory.Actions);
                _highlightingService.EnableHighlight(target, Color.yellow);
            }
            else
            {
                ConvaiLogger.Warn($"[{ActionName}] Failed: Target or Service missing.", ConvaiLogger.LogCategory.Actions);
            }

            yield return null; 

            // --- MODIFIED: Use the safe signal method on the handler ---
            _handler.SignalActionEnded(ActionName, target);
        }
    }
}