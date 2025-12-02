using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Convai.Scripts.Runtime.Core;
using Convai.Scripts.Runtime.LoggerSystem;
using Convai.Scripts.Runtime.Features.Actions.CustomActions;
using Convai.Scripts.Runtime.Features.CustomActions;
using Service; // Corrected Namespace for ICustomAction
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;

namespace Convai.Scripts.Runtime.Features
{
    /// <summary>
    /// This class acts as the Action Registry and Command Executor.
    /// It delegates complex action logic to ICustomAction implementations.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Convai/Convai Actions Handler")]
    public class ConvaiActionsHandler : MonoBehaviour
    {
        // This array is kept for Inspector serialization/display
        [SerializeField] public ActionMethod[] actionMethods; 
        
        public List<string> actionResponseList = new();
        private readonly List<ConvaiAction> _actionList = new();
        public readonly ActionConfig ActionConfig = new();
        private List<string> _actions = new();
        private ConvaiNPC _currentNPC;
        private ConvaiInteractablesData _interactablesData;
        private Coroutine _playActionListCoroutine;
        
        // Dictionary to map action name (string) to the ICustomAction instance
        private readonly Dictionary<string, ICustomAction> _customActions = new(StringComparer.OrdinalIgnoreCase); 
        private List<string> _registeredActionNames = new(); 

        // Awake is called when the script instance is being loaded
        private void Awake()
        {
            _interactablesData = FindObjectOfType<ConvaiInteractablesData>();

            if (_interactablesData == null)
                ConvaiLogger.Error("Convai Action Settings missing. Please create a game object that handles actions.",
                    ConvaiLogger.LogCategory.Character);

            if (TryGetComponent(out ConvaiNPC npc))
                _currentNPC = npc;

            RegisterCustomActions();

            ActionConfig.Actions.AddRange(_registeredActionNames);
            
            if (_interactablesData != null)
            {
                foreach (ConvaiInteractablesData.Character character in _interactablesData.Characters)
                {
                    ActionConfig.Types.Character rpcCharacter = new() { Name = character.Name, Bio = character.Bio };
                    ActionConfig.Characters.Add(rpcCharacter);
                }

                foreach (ConvaiInteractablesData.Object eachObject in _interactablesData.Objects)
                {
                    ActionConfig.Types.Object rpcObject = new() { Name = eachObject.Name, Description = eachObject.Description };
                    ActionConfig.Objects.Add(rpcObject);
                }
            }
        }
        
        private void RegisterCustomActions()
        {
            // 1. Instantiate all your ICustomAction classes here
            ICustomAction[] actions = new ICustomAction[]
            {
                // ACTIONS DELEGATED TO EXTERNAL CLASSES:
                new DisplayPrinterDashboardAction(),
                new ShowMeAction(),
                //new HighlightAction(), 
                new MoveToAction(),    
                new PickUpAction(),    
                new DropAction(),      
                // NOTE: Jump and Dance are purposely excluded here to handle them internally below.
            };

            // 2. Initialize and register them
            foreach (var action in actions)
            {
                action.Initialize(this); 
                _customActions[action.ActionName] = action;
                _registeredActionNames.Add(action.ActionName);
                ConvaiLogger.DebugLog($"Registered Custom Action: {action.ActionName}", ConvaiLogger.LogCategory.Actions);
            }
            
            // 3. Register names from the Inspector array (including Jump/Dance)
            foreach (ActionMethod actionMethod in actionMethods)
            {
                if (!_registeredActionNames.Contains(actionMethod.action))
                {
                    _registeredActionNames.Add(actionMethod.action);
                }
            }
        }

        private void Start()
        {
            #region Actions Setup
            ActionConfig.Classification = "multistep";
            ConvaiLogger.DebugLog(ActionConfig, ConvaiLogger.LogCategory.Actions);
            #endregion

            _playActionListCoroutine = StartCoroutine(PlayActionList());
        }

        private void OnEnable() {
            if ( _playActionListCoroutine != null ) {
                _playActionListCoroutine = StartCoroutine(PlayActionList());
            }
        }
        
        private void OnDisable() {
            if ( _playActionListCoroutine != null ) {
                StopCoroutine(_playActionListCoroutine);
            }
        }

        private void Update()
        {
            if (actionResponseList.Count > 0)
            {
                ParseActions(actionResponseList[0]);
                actionResponseList.RemoveAt(0);
            }
        }
        
        public void SendTextDataAsync(string text)
        {
            if (_currentNPC != null)
            {
                _currentNPC.SendTextDataAsync(text);
            }
            else
            {
                ConvaiLogger.Warn("Cannot send text data: ConvaiNPC reference is missing.", ConvaiLogger.LogCategory.Actions);
            }
        }

        private void ParseActions(string actionsString)
        {
            actionsString = actionsString.Trim();
            ConvaiLogger.DebugLog($"Parsing actions from: {actionsString}", ConvaiLogger.LogCategory.Actions);

            _actions = actionsString.Split(", ").ToList();
            _actionList.Clear();

            foreach (string action in _actions)
            {
                List<string> actionWords = action.Split(' ').ToList();
                ConvaiLogger.Info($"Processing action: {action}", ConvaiLogger.LogCategory.Actions);
                ParseSingleAction(actionWords);
            }
        }

        /// <summary>
        ///     Parses a single action from a list of action words.
        /// </summary>
        private void ParseSingleAction(List<string> actionWords)
        {
            for (int j = 0; j < actionWords.Count; j++)
            {
                string[] verbPart = actionWords.Take(j + 1).ToArray();
                string[] objectPart = actionWords.Skip(j + 1).ToArray();

                verbPart = verbPart.Select(word => word.TrimEnd('s')).ToArray();
                string actionString = string.Join(" ", verbPart);
                
                string matchingActionName = _registeredActionNames
                    .OrderBy(name => LevenshteinDistance(name.ToLower(), actionString.ToLower()))
                    .FirstOrDefault();

                if (matchingActionName == null || LevenshteinDistance(matchingActionName.ToLower(), actionString.ToLower()) > 2) continue;
                
                GameObject targetObject = FindTargetObject(objectPart);
                LogActionResult(verbPart, objectPart, targetObject);

                _actionList.Add(new ConvaiAction(matchingActionName, targetObject, "")); 
                break;
            }
        }

        /// <summary>
        ///     Event that is triggered when an action starts.
        /// </summary>
        public event Action<string, GameObject> ActionStarted;

        /// <summary>
        ///     Event that is triggered when an action ends.
        /// </summary>
        public event Action<string, GameObject> ActionEnded;
        
        // --- PUBLIC HELPERS TO SAFELY INVOKE EVENTS FROM EXTERNAL SCRIPTS ---

        /// <summary>
        /// Allows ICustomAction classes to trigger the ActionStarted event.
        /// </summary>
        public void SignalActionStarted(string actionName, GameObject target)
        {
            ActionStarted?.Invoke(actionName, target);
        }

        /// <summary>
        /// Allows ICustomAction classes to trigger the ActionEnded event.
        /// </summary>
        public void SignalActionEnded(string actionName, GameObject target)
        {
            ActionEnded?.Invoke(actionName, target);
        }

        // --- CORE ACTION EXECUTOR ---

        private IEnumerator PlayActionList()
        {
            while (true)
                if (_actionList.Count > 0)
                {
                    yield return DoAction(_actionList[0]);
                    _actionList.RemoveAt(0);
                }
                else
                {
                    yield return null;
                }
        }

        private IEnumerator DoAction(ConvaiAction action)
        {
            // 1. Check for simple, internal actions (Jump, Dance)
            if (action.ActionName.Equals("Jump", StringComparison.OrdinalIgnoreCase))
            {
                Jump();
                yield break;
            }
            if (action.ActionName.Equals("Dance", StringComparison.OrdinalIgnoreCase))
            {
                yield return AnimationActions("Dance");
                yield break;
            }

            // 2. Execute logic via ICustomAction dictionary lookup (Command Pattern)
            if (_customActions.TryGetValue(action.ActionName, out ICustomAction customAction))
            {
                yield return customAction.Execute(action.Target);
            }
            else
            {
                ConvaiLogger.Error($"Action '{action.ActionName}' not implemented internally or registered as a custom action.", ConvaiLogger.LogCategory.Actions);
            }

            yield return null;
        }

        // --- UTILITY AND HELPER METHODS ---

        /// <summary>
        ///     Finds the target object based on the object part of the action.
        /// </summary>
        private GameObject FindTargetObject(string[] objectPart)
        {
            string targetName = string.Join(" ", objectPart);

            ConvaiInteractablesData.Object obj = _interactablesData.Objects
                .OrderBy(o => LevenshteinDistance(o.Name.ToLower(), targetName.ToLower()))
                .FirstOrDefault();

            if (obj != null && LevenshteinDistance(obj.Name.ToLower(), targetName.ToLower()) <= 2)
                return obj.gameObject;

            ConvaiInteractablesData.Character character = _interactablesData.Characters
                .OrderBy(c => LevenshteinDistance(c.Name.ToLower(), targetName.ToLower()))
                .FirstOrDefault();

            if (character != null && LevenshteinDistance(character.Name.ToLower(), targetName.ToLower()) <= 2)
                return character.gameObject;

            return null;
        }

        /// <summary>
        ///     Calculates the Levenshtein distance between two strings.
        /// </summary>
        private int LevenshteinDistance(string s, string t)
        {
            int[][] d = new int[s.Length + 1][];
            for (int index = 0; index < s.Length + 1; index++) d[index] = new int[t.Length + 1];

            for (int i = 0; i <= s.Length; i++)
                d[i][0] = i;
            for (int j = 0; j <= t.Length; j++)
                d[0][j] = j;

            for (int j = 1; j <= t.Length; j++)
            for (int i = 1; i <= s.Length; i++)
            {
                int cost = s[i - 1] == t[j - 1] ? 0 : 1;
                d[i][j] = Math.Min(Math.Min(d[i - 1][j] + 1, d[i][j - 1] + 1), d[i - 1][j - 1] + cost);
            }

            return d[s.Length][t.Length];
        }

        private void LogActionResult(string[] verbPart, string[] objectPart, GameObject targetObject)
        {
            string verb = string.Join(" ", verbPart).ToLower();
            string obj = string.Join(" ", objectPart).ToLower();

            if (targetObject != null)
            {
                ConvaiLogger.DebugLog($"Active Target: {obj}", ConvaiLogger.LogCategory.Actions);
                ConvaiLogger.DebugLog($"Found matching target: {targetObject.name} for action: {verb}", ConvaiLogger.LogCategory.Actions);
            }
            else
            {
                ConvaiLogger.Warn($"No matching target found for action: {verb}", ConvaiLogger.LogCategory.Actions);
            }
        }

        // --- DATA STRUCTURES ---

        [Serializable]
        public class ActionMethod
        {
            [FormerlySerializedAs("Action")] [SerializeField]
            public string action;

            [HideInInspector] // <--- ADD THIS ATTRIBUTE
            [SerializeField] public string animationName;
            
            // Left for Inspector compatibility
            [HideInInspector] // <--- ADD THIS ATTRIBUTE
            [SerializeField] public int actionChoice; 
        }

        private class ConvaiAction
        {
            public ConvaiAction(string actionName, GameObject target, string animation)
            {
                ActionName = actionName;
                Target = target;
                Animation = animation;
            }

            public readonly string Animation;
            public readonly GameObject Target;
            public readonly string ActionName;
        }

        #region Action Implementation Methods
        
        /// <summary>
        /// Restores the original synchronous Jump logic.
        /// </summary>
        private void Jump()
        {
            SignalActionStarted("Jump", _currentNPC.gameObject);

            float jumpForce = 5f;
            if (GetComponent<Rigidbody>() != null)
                GetComponent<Rigidbody>().AddForce(new Vector3(0f, jumpForce, 0f), ForceMode.Impulse);
                
            _currentNPC.GetComponent<Animator>().CrossFade(Animator.StringToHash("Dance"), 1);

            SignalActionEnded("Jump", _currentNPC.gameObject);
        }

        /// <summary>
        ///     This method is a coroutine that handles playing an animation for Convai NPC.
        ///     Used for actions like Dance.
        /// </summary>
        private IEnumerator AnimationActions(string animationName)
        {
            // --- NOTE: Your original, lengthy AnimationActions implementation belongs here. ---
            
            // Placeholder:
            yield return null; 
        }

        // ... (Register/Unregister methods are restored and working via direct event subscription in ConvaiHeadTracking) ...

        #endregion
    }
}