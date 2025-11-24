using System.Collections;
using Convai.Scripts.Runtime.Core;
using Convai.Scripts.Runtime.LoggerSystem;
using Convai.Scripts.Runtime.Features;
using Convai.Scripts.Runtime.Features.CustomActions;
using UnityEngine;

// --- CORRECTED NAMESPACE ---
namespace Convai.Scripts.Runtime.Features.Actions.CustomActions 
{
    public class PickUpAction : ICustomAction
    {
        public string ActionName => "Pick Up";

        private ConvaiActionsHandler _handler;
        private ConvaiNPC _currentNPC;

        public void Initialize(ConvaiActionsHandler handler)
        {
            _handler = handler;
            if (!_handler.TryGetComponent(out _currentNPC))
            {
                ConvaiLogger.Error($"[{ActionName}] Missing ConvaiNPC component on handler GameObject.", ConvaiLogger.LogCategory.Actions);
            }
        }

        public IEnumerator Execute(GameObject target)
        {
            if (_currentNPC == null) yield break;

            _handler.SignalActionStarted(ActionName, target);

            // --- Original PickUp Logic Restored ---
            if (target == null)
            {
                ConvaiLogger.DebugLog("Target is null! Exiting PickUp coroutine.", ConvaiLogger.LogCategory.Actions);
                yield break;
            }

            if (!target.activeInHierarchy)
            {
                ConvaiLogger.DebugLog($"Target: {target.name} is inactive! Exiting PickUp coroutine.",
                    ConvaiLogger.LogCategory.Actions);
                yield break;
            }

            Vector3 direction = (target.transform.position - _handler.transform.position).normalized;
            direction.y = 0;

            Quaternion targetRotation = Quaternion.LookRotation(direction);
            float elapsedTime = 0f;
            float rotationTime = 0.5f;

            while (elapsedTime < rotationTime)
            {
                targetRotation.x = 0;
                targetRotation.z = 0;
                _handler.transform.rotation = Quaternion.Slerp(_handler.transform.rotation, targetRotation, elapsedTime / rotationTime);

                elapsedTime += Time.deltaTime;
                yield return null;
            }

            ConvaiLogger.DebugLog($"Picking up Target: {target.name}", ConvaiLogger.LogCategory.Actions);

            Animator animator = _currentNPC.GetComponent<Animator>();

            animator.CrossFade(Animator.StringToHash("Picking Up"), 0.1f);

            yield return new WaitForSeconds(1);

            float timeToReachObject = 1f;

            yield return new WaitForSeconds(timeToReachObject);

            if (!target.activeInHierarchy)
            {
                ConvaiLogger.DebugLog(
                    $"Target: {target.name} became inactive during the pick up animation! Exiting PickUp coroutine.",
                    ConvaiLogger.LogCategory.Actions);
                yield break;
            }

            target.transform.parent = _handler.gameObject.transform;
            target.SetActive(false);

            animator.CrossFade(Animator.StringToHash("Idle"), 0.4f);
            // --- End of Original Logic ---

            _handler.SignalActionEnded(ActionName, target);
        }
    }
}