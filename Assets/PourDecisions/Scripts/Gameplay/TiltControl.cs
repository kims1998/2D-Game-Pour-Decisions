using UnityEngine;

namespace PourDecisions {
    /*
    * Turns mouse or keyboard input into a bottle tilt angle (degrees, 0 = upright).
    * Mouse: grab anywhere outside the UI and drag right; drag distance maps to tilt angle.
    * Keyboard (hard mode): hold to tilt at a steady rate; let go and the bottle returns upright.
    */
    public class TiltControl {
        public const float MaxTilt = 165f;

        const float DragDegrees = 170f;               // tilt gained by dragging FullTiltScreenFraction (at 1x)
        const float FullTiltScreenFraction = 0.42f;  // share of the screen width
        const float KeyLiftRate = 140f;               // deg/s up to KeyLiftAngle
        const float KeyLiftAngle = 60f;
        const float KeyPourRate = 38f;                // deg/s past KeyLiftAngle, where the pouring happens
        const float TiltUpSpeed = 260f;               // how fast the bottle follows the target
        const float TiltDownSpeed = 320f;

        float target;
        bool dragging;
        float dragAnchorX;

        public float Tilt { get; private set; }

        public void Reset() {
            Tilt = target = 0f;
            dragging = false;
        }

        // True while serving or skipping: input is ignored and the bottle comes back up.
        public float Update(float dt, bool forceUpright) {
            if (forceUpright) {
                dragging = false;
                target = 0f;
            } else {
                if (!Settings.HardMode) ReadMouse();
                ReadKeyboard(dt);
            }

            // The bottle chases the target angle at a limited speed, so it feels like it has weight
            // instead of snapping to the mouse.
            float speed = target > Tilt ? TiltUpSpeed : TiltDownSpeed;
            Tilt = Mathf.MoveTowards(Tilt, target, speed * dt);

            return Tilt;
        }

        void ReadMouse() {
            // How many degrees of tilt one pixel of mouse movement gives. Scales with screen width
            // so the drag feels the same on any window size.
            float degPerPixel = DragDegrees / (FullTiltScreenFraction * Mathf.Max(640, Screen.width)) * Settings.Sensitivity;

            // Mouse went down somewhere that is not a button: start dragging.
            if (PourInput.PointerPressed && !PourInput.PointerOverUI) {
                dragging = true;
                // Anchor so grabbing a tilted bottle never makes it jump.
                dragAnchorX = PourInput.PointerPosition.x - Tilt / degPerPixel;
            }

            if (dragging && !PourInput.PointerHeld) dragging = false;
            // While dragging, the target tilt is how far right of the anchor the mouse is.
            if (dragging) target = Mathf.Clamp((PourInput.PointerPosition.x - dragAnchorX) * degPerPixel, 0f, MaxTilt);
        }

        void ReadKeyboard(float dt) {
            if (PourInput.TiltKeyHeld) {
                // Keyboard: tilt fast until the bottle is lifted, then slowly while it pours, for finer control.
                float rate = target < KeyLiftAngle ? KeyLiftRate : KeyPourRate;
                target = Mathf.Min(MaxTilt, Mathf.Max(target, Tilt) + rate * dt);
            } else if (!dragging) {
                target = 0f;
            }
        }
    }
}