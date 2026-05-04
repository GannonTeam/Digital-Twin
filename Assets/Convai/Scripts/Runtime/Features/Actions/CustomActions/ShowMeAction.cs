using System.Collections;
using Convai.Scripts.Runtime.LoggerSystem;
using Convai.Scripts.Runtime.Features;
using UnityEngine;
using System.Linq;
using Convai.Scripts.Runtime.Features.CustomActions; // Needed for linq queries if you use them, but often just for ICustomAction lookup

namespace Convai.Scripts.Runtime.Features.Actions.CustomActions
{
    public class ShowMeAction : ICustomAction
    {
        public string ActionName => "Show Me";

        private ConvaiActionsHandler _handler;
        
        // References to other action classes for delegation
        private ICustomAction _moveToAction;
        private ICustomAction _highlightAction;

        public void Initialize(ConvaiActionsHandler handler)
        {
            _handler = handler;
            
            // NOTE: Since the ConvaiActionsHandler holds all actions in a private dictionary, 
            // the cleanest way to access them is through helper methods in the handler or by 
            // directly instantiating them here, assuming they are light classes (which they are).
            
            // To ensure orchestration is possible, we create and initialize the dependencies here:
            
            // 1. Get MoveToAction instance
            _moveToAction = new MoveToAction();
            _moveToAction.Initialize(handler);

            // 2. Get HighlightAction instance
            _highlightAction = new HighlightAction();
            _highlightAction.Initialize(handler);
        }

        public IEnumerator Execute(GameObject target)
        {
            if (target == null)
            {
                ConvaiLogger.Warn($"[{ActionName}] Failed: Target object is null.", ConvaiLogger.LogCategory.Actions);
                yield break;
            }

            _handler.SignalActionStarted(ActionName, target);
            
            ConvaiLogger.DebugLog($"Executing Show Me on {target.name}: Starting MoveTo.", ConvaiLogger.LogCategory.Actions);

            // --- STEP 1: Move to the target ---
            if (_moveToAction != null)
            {
                // Execute MoveTo. We must yield return the entire coroutine.
                // NOTE: We wrap the MoveTo execution to control the sequence, 
                // but we rely on the MoveToAction to handle its own start/end signaling.
                yield return _moveToAction.Execute(target); 
            }
            else
            {
                ConvaiLogger.Error($"[{ActionName}] MoveToAction is null. Cannot move.", ConvaiLogger.LogCategory.Actions);
                yield break;
            }
            
            ConvaiLogger.DebugLog($"Executing Show Me on {target.name}: Starting Highlight.", ConvaiLogger.LogCategory.Actions);

            // --- STEP 2: Highlight the target (This is instant/short) ---
            if (_highlightAction != null)
            {
                // Execute Highlight. This is a short coroutine.
                yield return _highlightAction.Execute(target);
            }
            else
            {
                ConvaiLogger.Error($"[{ActionName}] HighlightAction is null. Cannot highlight.", ConvaiLogger.LogCategory.Actions);
                yield break;
            }

            ConvaiLogger.DebugLog($"Executing Show Me on {target.name}: Completed.", ConvaiLogger.LogCategory.Actions);
            
            _handler.SignalActionEnded(ActionName, target);
        }
    }
}