using System;
using Convai.Scripts.Runtime.LoggerSystem;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Convai.Scripts.Runtime.Core
{
    [DefaultExecutionOrder(-105)]
    public class ConvaiInputManager : MonoBehaviour
#if ENABLE_INPUT_SYSTEM
        , Controls.IPlayerActions // NOTE: Assuming 'Controls' is the necessary alias here.
#endif
    {
        [HideInInspector] public Vector2 moveVector;
        [HideInInspector] public Vector2 lookVector;
        public bool isRunning { get; private set; }

        public Action jumping;
        public Action sendText;
        public Action toggleChat;
        public Action toggleSettings;

        public bool IsTalkKeyHeld { get; private set; }
        public Action<bool> talkKeyInteract;

        // *** ADDED: State variable for right-click look ***
        public bool IsLookingHeld { get; private set; } 
        
#if ENABLE_INPUT_SYSTEM
        private Controls _controls;
#elif ENABLE_LEGACY_INPUT_MANAGER
        [Serializable]
        public class MovementKeys
        {
            public const KeyCode Forward = KeyCode.W;
            public const KeyCode Backward = KeyCode.S;
            public const KeyCode Right = KeyCode.D;
            public const KeyCode Left = KeyCode.A;
        }

        public KeyCode TextSendKey = KeyCode.Return;
        public KeyCode TextSendAltKey = KeyCode.KeypadEnter;
        public KeyCode TalkKey = KeyCode.T;
        public KeyCode OpenSettingPanelKey = KeyCode.F10;
        public KeyCode RunKey = KeyCode.LeftShift;
        public MovementKeys movementKeys;
        
        public bool WasTalkKeyPressed()
        {
            return Input.GetKeyDown(TalkKey);
        }
#endif

        public static ConvaiInputManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null)
            {
                ConvaiLogger.DebugLog("There's more than one ConvaiInputManager! " + transform + " - " + Instance, ConvaiLogger.LogCategory.UI);
                Destroy(gameObject);
                return;
            }

            Instance = this;
            // *** MODIFIED: Set cursor to visible and unlocked by default ***
            LockCursor(false); 
        }

        private void OnEnable()
        {
#if ENABLE_INPUT_SYSTEM
            _controls = new Controls();
            _controls.Player.SetCallbacks(this);
            _controls.Enable();
#endif
        }

        private void OnDisable()
        {
#if ENABLE_INPUT_SYSTEM
            _controls.Disable();
#endif
        }

#if ENABLE_INPUT_SYSTEM
        // KEEPING ALL INTERFACE METHODS REQUIRED BY InputSystem_Actions.IPlayerActions
        // (This assumes you added the 6 missing placeholder methods from the last step)

        public void OnJump(InputAction.CallbackContext context)
        {
            if (context.performed) jumping?.Invoke();
        }

        public void OnMove(InputAction.CallbackContext context)
        {
            moveVector = context.ReadValue<Vector2>();
        }

        public void OnLook(InputAction.CallbackContext context)
        {
            lookVector = context.ReadValue<Vector2>();
        }
        
        // --- MISSING INTERFACE METHODS (Placeholder required for compilation) ---
        // The compiler requires these to be present.
        public void OnAttack(InputAction.CallbackContext context) { /* Required by interface */ }
        public void OnCrouch(InputAction.CallbackContext context) { /* Required by interface */ }
        public void OnInteract(InputAction.CallbackContext context) { /* Required by interface */ }
        public void OnNext(InputAction.CallbackContext context) { /* Required by interface */ }
        public void OnPrevious(InputAction.CallbackContext context) { /* Required by interface */ }
        public void OnSprint(InputAction.CallbackContext context) { /* Required by interface */ }
        // ----------------------------------------------------------------------

        public void OnMousePress(InputAction.CallbackContext context)
        {
        }

        public void OnRun(InputAction.CallbackContext context)
        {
            if (context.performed) isRunning = !isRunning;
        }

        public void OnSendText(InputAction.CallbackContext context)
        {
            if (context.performed) sendText?.Invoke();
        }

        public void OnToggleChat(InputAction.CallbackContext context)
        {
            if (context.performed) toggleChat?.Invoke();
        }

        public void OnToggleSettings(InputAction.CallbackContext context)
        {
            if (context.performed) toggleSettings?.Invoke();
        }

        public void OnTalk(InputAction.CallbackContext context)
        {
            if (context.performed)
            {
                talkKeyInteract?.Invoke(true);
                IsTalkKeyHeld = true;
            }

            if (context.canceled)
            {
                talkKeyInteract?.Invoke(false);
                IsTalkKeyHeld = false;
            }
        }

        // *** ADDED: Right-Click Look Action Implementation ***
        public void OnRightClickLook(InputAction.CallbackContext context)
        {
            if (context.performed)
            {
                IsLookingHeld = true;
            }
            else if (context.canceled)
            {
                IsLookingHeld = false;
                // Unlocking the cursor is handled in PlayerMovement Update, 
                // but this ensures the state is clear.
            }
        }
        // ******************************************************

        public void OnCursorUnlock(InputAction.CallbackContext context)
        {
            // *** MODIFIED: Delete default Escape key unlock logic ***
        }
#endif

        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            // *** REMOVED: Default click-to-lock logic ***
            // The mouse is now unlocked by default.
            
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetButton("Jump"))
            {
                jumping?.Invoke();
            }
// ... (rest of legacy code) ...
#endif
        }

        private static void LockCursor(bool lockState)
        {
            // MODIFIED: Cursor lock/hide is ONLY used temporarily when right-click is held
            Cursor.lockState = lockState ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !lockState;
        }

#if ENABLE_INPUT_SYSTEM
        public InputAction GetTalkKeyAction()
        {
            // NOTE: Must access the property name defined in the generated C# file.
            // Assuming the action is named 'Talk' in the asset.
            return _controls.Player.Talk; 
        }
#endif
    }
}