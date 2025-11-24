using System.Collections;
using Convai.Scripts.Runtime.Core;
using Convai.Scripts.Runtime.LoggerSystem;
using Convai.Scripts.Runtime.Features;
using Convai.Scripts.Runtime.Features.CustomActions;
using UnityEngine;
using UnityEngine.AI;

namespace Convai.Scripts.Runtime.Features.Actions.CustomActions
{
    public class MoveToAction : ICustomAction
    {
        public string ActionName => "Move To";

        private ConvaiActionsHandler _handler;
        private ConvaiNPC _currentNPC;

        public void Initialize(ConvaiActionsHandler handler)
        {
            _handler = handler;
            // Assuming ConvaiNPC is set in the handler's Awake method
            if (!_handler.TryGetComponent(out _currentNPC))
            {
                ConvaiLogger.Error($"[{ActionName}] Missing ConvaiNPC component on handler GameObject.", ConvaiLogger.LogCategory.Actions);
            }
        }

        public IEnumerator Execute(GameObject target)
        {
            if (_currentNPC == null) yield break;
            
            _handler.SignalActionStarted(ActionName, target);

            // --- Original MoveTo Logic Restored ---
            if (!IsTargetValid(target)) yield break;

            ConvaiLogger.DebugLog($"Moving to Target: {target.name}", ConvaiLogger.LogCategory.Actions);

            Animator animator = _currentNPC.GetComponent<Animator>();
            NavMeshAgent navMeshAgent = _currentNPC.GetComponent<NavMeshAgent>();

            SetupAnimationAndNavigation(animator, navMeshAgent);

            Vector3 targetDestination = CalculateTargetDestination(target);
            navMeshAgent.SetDestination(targetDestination);
            yield return null;

            yield return MoveTowardsTarget(target, navMeshAgent);

            FinishMovement(animator, target);
            // --- End of Original Logic ---

            _handler.SignalActionEnded(ActionName, target);
        }

        // --- Original Helper Methods (Needed inside the class now) ---

        private bool IsTargetValid(GameObject target)
        {
            if (target == null || !target.activeInHierarchy)
            {
                ConvaiLogger.DebugLog("MoveTo target is null or inactive.", ConvaiLogger.LogCategory.Actions);
                return false;
            }
            return true;
        }

        private void SetupAnimationAndNavigation(Animator animator, NavMeshAgent navMeshAgent)
        {
            animator.CrossFade(Animator.StringToHash("Walking"), 0.01f);
            animator.applyRootMotion = false;
            navMeshAgent.updateRotation = false;
        }

        private Vector3 CalculateTargetDestination(GameObject target)
        {
            Vector3 targetDestination = target.transform.position;
            if (target.TryGetComponent(out Renderer rendererComponent))
            {
                float zOffset = rendererComponent.bounds.size.z;
                targetDestination += zOffset * target.transform.forward;
            }
            else
            {
                targetDestination += 0.5f * target.transform.forward;
            }
            return targetDestination;
        }

        private IEnumerator MoveTowardsTarget(GameObject target, NavMeshAgent navMeshAgent)
        {
            float rotationSpeed = 5;
            while (navMeshAgent.remainingDistance > navMeshAgent.stoppingDistance)
            {
                if (!target.activeInHierarchy)
                {
                    ConvaiLogger.DebugLog("Target deactivated during movement.", ConvaiLogger.LogCategory.Actions);
                    yield break;
                }

                if (navMeshAgent.velocity.sqrMagnitude < Mathf.Epsilon) yield return null;

                RotateTowardsMovementDirection(navMeshAgent, rotationSpeed);
                yield return null;
            }
        }

        private void RotateTowardsMovementDirection(NavMeshAgent navMeshAgent, float rotationSpeed)
        {
            Quaternion rotation = Quaternion.LookRotation(navMeshAgent.velocity.normalized);
            rotation.x = 0;
            rotation.z = 0;
            _handler.transform.rotation = Quaternion.Slerp(_handler.transform.rotation, rotation, rotationSpeed * Time.deltaTime);
        }

        private void FinishMovement(Animator animator, GameObject target)
        {
            animator.CrossFade(Animator.StringToHash("Idle"), 0.1f);
            // Assuming the RotateTowardsCamera Coroutine needs to run on the Handler object
            // You might need to add a public method in ConvaiActionsHandler to call this StartCoroutine
            // For now, removing the StartCoroutine call to maintain clean encapsulation:
            // if (_actions.Count == 1 && Camera.main != null) _handler.StartRotateTowardsCamera(); 
            animator.applyRootMotion = true;
        }
    }
}