using UnityEngine;

namespace PourDecisions {
    /*
    * The set: background, counter, lighting and the camera.
    * While the player pours, the camera eases in on the glass so even a thin layer in a pint glass
    * is readable. Dim levels darken the room and leave a pool of light over the glass.
    */
    public class Stage : MonoBehaviour {
        public Camera Cam { get; private set; }
        public Color Ambient { get; private set; } = Color.white;

        // 0 = wide shot, 1 = close on the glass. Gameplay sets this from the tilt.
        public float Focus { get; set; }
        public Glass FocusGlass { get; set; }

        SpriteRenderer background, counter, darkness;
        float camSize = K.BaseOrtho;
        Vector2 camPos;

        public void Build(Camera cam) {
            Cam = cam;
            cam.orthographic = true;
            cam.orthographicSize = K.BaseOrtho;
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.transform.rotation = Quaternion.identity;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.05f, 0.05f);

            // The set is just sprites. Layers (in Art.cs) gives each a sorting order, which decides what draws in front.
            background = Art.NewSprite("Background", transform, null, Layers.Background);
            counter = Art.NewSprite("Counter", transform, null, Layers.Counter);
            counter.transform.position = new Vector3(0f, K.CounterBackY, 0f);
            darkness = Art.NewSprite("Darkness", transform, Art.Sprite("fx_vignette"), Layers.Darkness);
            darkness.gameObject.SetActive(false);
            background.sprite = Art.Sprite("bg_bar");
            counter.sprite = Art.Sprite("counter_bar", 100f, new Vector2(0.5f, 1f));
        }

        // 0 = normal lighting, 1 = nearly dark. Returns the ambient tint other objects should use.
        public Color SetDim(float amount) {
            float dim = Mathf.Clamp01(amount);
            // Dim levels tint everything toward a dark purple. Every sprite multiplies its color by Ambient.
            Ambient = Color.Lerp(Color.white, new Color(0.38f, 0.34f, 0.45f, 1f), dim);
            background.color = Ambient;
            counter.color = Ambient;
            darkness.gameObject.SetActive(dim > 0.01f);
            darkness.color = new Color(1f, 1f, 1f, Mathf.Clamp01(dim * 1.3f));
            if (Fx.I != null) Fx.I.Ambient = Ambient;

            return Ambient;
        }

        void LateUpdate() => Layout(Time.unscaledDeltaTime);

        // Fits the set to the screen and eases the camera toward its target framing (dt = seconds since last frame).
        public void Layout(float dt) {
            if (Cam == null) return;
            float aspect = Mathf.Max(0.5f, Cam.aspect);

            // Stretch the set for ultra-wide screens so no edge ever shows.
            // Scale the background up on very wide windows so no edge shows.
            float s = Mathf.Max(1f, aspect * K.BaseOrtho / 9.6f);
            background.transform.localScale = new Vector3(s, s, 1f);
            counter.transform.localScale = new Vector3(s, 1f, 1f);

            // Camera framing: wide shot, or a close-up that keeps the spout and the glass in view.
            float targetSize = K.BaseOrtho;
            Vector2 targetPos = Vector2.zero;

            if (FocusGlass != null && Focus > 0f) {
                float gh = FocusGlass.HeightUnits;
                // Close-up: a smaller orthographic size zooms in. The camera also moves to sit near the glass.
                float closeSize = Mathf.Clamp(0.5f * (gh + 3.8f), 3.4f, 4.8f);
                Vector2 closePos = new Vector2(FocusGlass.CenterX - 1.0f, K.CounterY + gh * 0.5f + 1.0f);
                targetSize = Mathf.Lerp(K.BaseOrtho, closeSize, Focus);
                targetPos = Vector2.Lerp(Vector2.zero, closePos, Focus);
            }

            float k = 1f - Mathf.Exp(-dt / 0.35f);
            // Ease toward the target instead of cutting, so the zoom feels smooth.
            camSize += (targetSize - camSize) * k;
            camPos += (targetPos - camPos) * k;

            // Keep the view inside the background.
            float halfW = camSize * aspect, halfH = camSize;
            // Never let the camera see past the edge of the background image.
            float limitX = Mathf.Max(0f, 9.6f * s - halfW);
            float limitY = Mathf.Max(0f, 5.4f * s - halfH);
            camPos.x = Mathf.Clamp(camPos.x, -limitX, limitX);
            camPos.y = Mathf.Clamp(camPos.y, -limitY, limitY);
            Cam.orthographicSize = camSize;
            Cam.transform.position = new Vector3(camPos.x, camPos.y, -10f);

            if (darkness.gameObject.activeSelf) {
                // Pool of light centred on the glass.
                Vector2 c = FocusGlass != null ? new Vector2(FocusGlass.CenterX, K.CounterY + FocusGlass.HeightUnits * 0.5f) : camPos;
                darkness.transform.position = new Vector3(c.x, c.y, 0f);
                float w = halfW * 2f * 1.9f, h = halfH * 2f * 1.9f;
                darkness.transform.localScale = new Vector3(w / 5.12f, h / 2.88f, 1f);
            }
        }

        // Snap back to the wide shot (between screens).
        public void ResetCamera() {
            Focus = 0f;
            FocusGlass = null;
            camSize = K.BaseOrtho;
            camPos = Vector2.zero;
        }
    }
}