using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace PourDecisions {
    /*
    * One place that reads the mouse and keyboard, so the game works whether the project uses the
    * new Input System package or the legacy Input Manager.
    */
    public static class PourInput {
        // Unity has an old Input Manager and a newer Input System package, and projects use one or the other.
        // The #if blocks pick the right calls, so the rest of the game never has to care.
        public static Vector2 PointerPosition {
            get {
            #if ENABLE_INPUT_SYSTEM
                return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
                #else
                return Input.mousePosition;
                #endif
            }
        }

        public static bool PointerPressed {
            get {
                #if ENABLE_INPUT_SYSTEM
                return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
                #else
                return Input.GetMouseButtonDown(0);
                #endif
            }
        }

        public static bool PointerHeld {
            get {
                #if ENABLE_INPUT_SYSTEM
                return Mouse.current != null && Mouse.current.leftButton.isPressed;
                #else
                return Input.GetMouseButton(0);
                #endif
            }
        }

        // Hard-mode tilt: Space, Right Arrow or D.
        public static bool TiltKeyHeld {
            get {
                #if ENABLE_INPUT_SYSTEM
                var k = Keyboard.current;
                return k != null && (k.spaceKey.isPressed || k.rightArrowKey.isPressed || k.dKey.isPressed);
                #else
                return Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D);
                #endif
            }
        }

        // Serve the drink: Enter or E.
        public static bool ServePressed {
            get {
                #if ENABLE_INPUT_SYSTEM
                var k = Keyboard.current;
                return k != null && (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame ||
                                     k.eKey.wasPressedThisFrame);
                #else
                return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) ||
                       Input.GetKeyDown(KeyCode.E);
                #endif
            }
        }

        // Pause: Escape or P.
        public static bool PausePressed {
            get {
                #if ENABLE_INPUT_SYSTEM
                var k = Keyboard.current;
                return k != null && (k.escapeKey.wasPressedThisFrame || k.pKey.wasPressedThisFrame);
                #else
                return Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P);
                #endif
            }
        }

        // Training mode: skip to a new order with Tab.
        public static bool SkipPressed {
            get {
                #if ENABLE_INPUT_SYSTEM
                var k = Keyboard.current;
                return k != null && k.tabKey.wasPressedThisFrame;
                #else
                return Input.GetKeyDown(KeyCode.Tab);
                #endif
            }
        }

        // True when the mouse is over a UI element (so clicking a button never starts a pour).
        public static bool PointerOverUI =>
            EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }
}
