using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PourDecisions {
    // Palette used across every screen.
    public static class Palette {
        public static readonly Color Ink = new Color(0.16f, 0.1f, 0.08f);
        public static readonly Color Cream = new Color(1f, 0.96f, 0.88f);
        public static readonly Color Amber = new Color(1f, 0.74f, 0.25f);
        public static readonly Color Teal = new Color(0.2f, 0.62f, 0.6f);
        public static readonly Color Red = new Color(0.85f, 0.3f, 0.25f);
        public static readonly Color Green = new Color(0.36f, 0.72f, 0.4f);
        public static readonly Color Slate = new Color(0.25f, 0.24f, 0.3f);
        public static readonly Color Panel = new Color(0.12f, 0.09f, 0.1f, 0.92f);
        public static readonly Color Dim = new Color(0f, 0f, 0f, 0.6f);
    }

    // Helpers that build uGUI elements from code with consistent styling.
    public static class UIKit {
        public static RectTransform Rect(string name, Transform parent) {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            return (RectTransform)go.transform;
        }

        // Positions a rect: anchor point (0..1), pivot (0..1), offset in reference pixels, size.
        public static RectTransform Place(this RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size) {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            return rt;
        }

        public static RectTransform Stretch(this RectTransform rt, float inset = 0f) {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);

            return rt;
        }

        public static Image Image(Transform parent, string name, Sprite sprite, Color color, bool sliced = false) {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            if (sliced) img.type = UnityEngine.UI.Image.Type.Sliced;
            else img.preserveAspect = true;

            return img;
        }

        // Rounded panel (9-sliced).
        public static Image Panel(Transform parent, string name, Color color) {
            var img = Image(parent, name, Art.Sliced("ui_panel", 30f), color, true);
            img.raycastTarget = true;

            return img;
        }

        // Solid full-screen blocker (for dimming behind menus).
        public static Image Blocker(Transform parent, Color color) {
            var img = Image(parent, "Blocker", Art.Sprite("fx_white"), color, true);
            img.type = UnityEngine.UI.Image.Type.Simple;
            img.preserveAspect = false;
            img.raycastTarget = true;
            img.rectTransform.Stretch();

            return img;
        }

        public static Text Label(Transform parent, string text, Font font, int size, Color color,
            TextAnchor align = TextAnchor.MiddleCenter) {
            var rt = Rect("Text", parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.supportRichText = true;
            
            return t;
        }

        // Soft drop shadow so text reads over busy art.
        public static T Shadowed<T>(this T graphic, float distance = 3f, float alpha = 0.6f) where T : Graphic {
            var s = graphic.gameObject.AddComponent<Shadow>();
            s.effectColor = new Color(0f, 0f, 0f, alpha);
            s.effectDistance = new Vector2(distance, -distance);

            return graphic;
        }

        public static T Outlined<T>(this T graphic, Color color, float distance = 3f) where T : Graphic {
            var o = graphic.gameObject.AddComponent<Outline>();
            o.effectColor = color;
            o.effectDistance = new Vector2(distance, -distance);
            return graphic;
        }

        // Chunky rounded button with hover/press feedback and click sound.
        public static Button Button(Transform parent, string label, Color color, Action onClick, int fontSize = 40) {
            var img = Image(parent, "Button_" + label, Art.Sliced("ui_button", 32f), color, true);
            img.raycastTarget = true;
            var b = img.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            var cb = b.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1f, 1f, 1f, 1f);
            cb.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            cb.selectedColor = Color.white;
            cb.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.6f);
            b.colors = cb;
            // No keyboard navigation/selection: Space and the arrow keys are tilt controls, and a
            // selected button would otherwise "submit" when Space is pressed.
            b.navigation = new Navigation { mode = Navigation.Mode.None };
            b.onClick.AddListener(() => {
                AudioManager.I.Click();
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
                onClick?.Invoke();
            });
            var t = Label(img.transform, label, Art.Display, fontSize, Color.white);
            t.rectTransform.Stretch();
            t.rectTransform.offsetMin = new Vector2(10f, 6f);
            t.rectTransform.offsetMax = new Vector2(-10f, 0f);
            t.Shadowed(2f, 0.45f);
            img.gameObject.AddComponent<ButtonFx>();
            
            return b;
        }

        public static Text LabelOf(Button b) => b.GetComponentInChildren<Text>();
    }

    // Grows a button a little on hover and plays a soft tick.
    public class ButtonFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler {
        float target = 1f, current = 1f;
        Button button;

        void Awake() => button = GetComponent<Button>();

        public void OnPointerEnter(PointerEventData e) {
            if (button != null && !button.interactable) return;
            target = 1.06f;
            AudioManager.I.Hover();
        }

        public void OnPointerExit(PointerEventData e) => target = 1f;

        void OnDisable() {
            target = current = 1f;
            transform.localScale = Vector3.one;
        }

        void Update() {
            current = Mathf.MoveTowards(current, target, Time.unscaledDeltaTime * 1.2f);
            transform.localScale = new Vector3(current, current, 1f);
        }
    }
}