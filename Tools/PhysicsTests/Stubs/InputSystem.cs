#if ENABLE_INPUT_SYSTEM
namespace UnityEngine.InputSystem {
    namespace Controls {
        public class ButtonControl { public bool wasPressedThisFrame => false; public bool isPressed => false; }
        public class Vector2Control { public Vector2 ReadValue() => default; }
        public class KeyControl : ButtonControl { }
    }
    public class Mouse {
        public static Mouse current => null;
        public Controls.Vector2Control position => null;
        public Controls.ButtonControl leftButton => null;
    }
    public class Keyboard {
        public static Keyboard current => null;
        public Controls.KeyControl spaceKey, rightArrowKey, dKey, enterKey, numpadEnterKey, eKey, escapeKey, pKey, tabKey;
    }
}
namespace UnityEngine.InputSystem.UI {
    public class InputSystemUIInputModule : UnityEngine.EventSystems.BaseInputModule { public void AssignDefaultActions() { } }
}
#endif
