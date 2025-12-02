using System.Collections;
using Convai.Scripts.Runtime.LoggerSystem;
using Convai.Scripts.Runtime.Features;
using UnityEngine;
using Convai.Scripts.Runtime.Custom;
using Convai.Scripts.Runtime.Features.CustomActions;

namespace Convai.Scripts.Runtime.Features.Actions.CustomActions
{
    public class DisplayPrinterDashboardAction : ICustomAction
    {
        public string ActionName => "Display Printer Dashboard"; 

        private ConvaiActionsHandler _handler;

        public void Initialize(ConvaiActionsHandler handler)
        {
            _handler = handler;
        }

        public IEnumerator Execute(GameObject target)
        {
            if (target == null)
            {
                ConvaiLogger.Warn($"[{ActionName}] Target object is null. Cannot open dashboard.", ConvaiLogger.LogCategory.Actions);
                yield break;
            }

            _handler.SignalActionStarted(ActionName, target);
            
            // --- DEBUG LOG START ---
            ConvaiLogger.DebugLog($"[{ActionName}] Step 1: Action received for target '{target.name}'.", ConvaiLogger.LogCategory.Actions);

            // 1. Get the Toggle Component
            if (!target.TryGetComponent<ClickableDashboardToggle>(out var toggleComponent))
            {
                ConvaiLogger.Error($"[{ActionName}] Step 2: FAILED. Target '{target.name}' does not have ClickableDashboardToggle component.", ConvaiLogger.LogCategory.Actions);
                _handler.SignalActionEnded(ActionName, target);
                yield break;
            }
            
            string printerId = toggleComponent.PrinterId;

            ConvaiLogger.DebugLog($"[{ActionName}] Step 2: Component found. Calling OpenDashboard(ID: {printerId}).", ConvaiLogger.LogCategory.Actions);
            
            // 2. OPEN THE DASHBOARD (Core Instruction)
            toggleComponent.OpenDashboard(printerId); 

            // 3. Finalize Action
            yield return null; 

            ConvaiLogger.DebugLog($"[{ActionName}] Step 3: Command finished.", ConvaiLogger.LogCategory.Actions);
            // --- DEBUG LOG END ---
            
            _handler.SignalActionEnded(ActionName, target);
        }
    }
}