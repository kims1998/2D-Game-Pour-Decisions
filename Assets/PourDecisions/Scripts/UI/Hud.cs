using System;
using UnityEngine;
using UnityEngine.UI;

namespace PourDecisions {
    /*
    * In-shift interface. Owns the title, serve/skip/pause buttons, controls hint and Training readout,
    * and delegates the bigger pieces to widgets in HudWidgets.cs (ticket, score board, speech bubble,
    * grade popup, banners). ShiftController only talks to this class.
    */
    public class Hud : MonoBehaviour {
        public event Action ServeClicked;
        public event Action SkipClicked;
        public string TitleText { get; private set; }

        RectTransform root;
        GameMode mode;
        int totalOrders;

        Text title, subtitle;
        Button serve, skip;
        RectTransform hintPanel, livePanel;
        Text hintText, liveText;

        OrderTicket ticket;
        ScoreBoard board;
        SpeechBubble bubble;
        GradePopup grade;
        Announcer announcer;

        public void Build(RectTransform canvasRoot, Camera cam) {
            // This component lives on its own object (not under root), so hiding the HUD never stops its coroutines.
            root = UIKit.Rect("HUD", canvasRoot).Stretch();
            ticket = new OrderTicket(this, root);
            BuildTitle();
            board = new ScoreBoard(this, root);
            BuildBottom();
            bubble = new SpeechBubble(this, root, cam);
            grade = new GradePopup(this, root);
            announcer = new Announcer(this, root);
            SetVisible(false);
        }

        void BuildTitle() {
            title = UIKit.Label(root, "", Art.Display, 50, Palette.Cream).Shadowed(3f, 0.7f);
            title.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(900f, 64f));
            subtitle = UIKit.Label(root, "", Art.Body, 28, Palette.Cream).Shadowed(2f, 0.7f);
            subtitle.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -76f), new Vector2(900f, 40f));
        }

        void BuildBottom() {
            serve = UIKit.Button(root, "SERVE  (Enter)", Palette.Amber, () => ServeClicked?.Invoke(), 40);
            ((RectTransform)serve.transform).Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 28f), new Vector2(360f, 96f));
            skip = UIKit.Button(root, "SKIP (Tab)", Palette.Slate, () => SkipClicked?.Invoke(), 32);
            ((RectTransform)skip.transform).Place(new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(200f, 36f), new Vector2(220f, 80f));
            var pause = UIKit.Button(root, "II", Palette.Slate, () => GameRoot.I.TogglePause(), 40);
            ((RectTransform)pause.transform).Place(new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 24f), new Vector2(88f, 88f));

            hintPanel = InfoPanel("Hint", new Vector2(0f, 0f), new Vector2(24f, 24f), new Vector2(560f, 120f), Art.Body, 26, 12f, out hintText);
            livePanel = InfoPanel("Live", new Vector2(1f, 0f), new Vector2(-128f, 24f), new Vector2(380f, 110f), Art.BodyHeavy, 28, 10f, out liveText);
        }

        // Translucent panel in a screen corner with one block of text.
        RectTransform InfoPanel(string name, Vector2 corner, Vector2 pos, Vector2 size, Font font, int fontSize, float inset, out Text text) {
            var panel = UIKit.Panel(root, name, new Color(0f, 0f, 0f, 0.65f));
            panel.raycastTarget = false;
            panel.rectTransform.Place(corner, corner, pos, size);
            text = UIKit.Label(panel.rectTransform, "", font, fontSize, Color.white);
            text.rectTransform.Stretch(inset);

            return panel.rectTransform;
        }

        public void SetVisible(bool visible) => root.gameObject.SetActive(visible);

        // ------------------------------------------------------------ API used by ShiftController

        public void BeginShift(GameMode m, string titleText, int orders) {
            mode = m;
            totalOrders = orders;
            TitleText = titleText;
            title.text = titleText;
            subtitle.text = m == GameMode.Classic ? "Bar shift" : "Training";
            ticket.Begin(m);
            skip.gameObject.SetActive(m == GameMode.Training);
            livePanel.gameObject.SetActive(false);
            grade.Hide();
            announcer.HideAll();
            bubble.Hide();
            board.Reset();
            SetServeEnabled(false);
            SetVisible(true);
        }

        public void SetOrder(Order o, int index, GameMode m) => ticket.Show(o, index, totalOrders, m);
        public void SetTimer(float fraction, float seconds) => ticket.SetTimer(fraction, seconds);
        public void SetScore(int score) => board.SetScore(score);
        public void SetStreak(int streak) => board.SetStreak(streak);
        public void SetTips(float total, bool animate) => board.SetTips(total, animate);
        public void SetServeEnabled(bool on) => serve.interactable = on;
        public void ShowBubble(string text, Customer who) => bubble.Show(text, who);
        public void HideBubble() => bubble.Hide();
        public void ShowGrade(PourResult r) => grade.Show(r);
        public void Banner(string big, string small, float duration) => announcer.Banner(big, small, duration);
        public void Flash(string text) => announcer.Flash(text);

        // Training readout. Pass poured < 0 to hide.
        public void SetLive(float poured, float target, float flow) {
            bool show = poured >= 0f && mode == GameMode.Training;
            livePanel.gameObject.SetActive(show);
            if (show) liveText.text = $"Poured  {poured:0.0} / {target:0.0} ml\n<size=22>Flow {flow:0} ml/s</size>";
        }

        public void ShowHint(bool show, bool keyboard) {
            hintPanel.gameObject.SetActive(show);
            hintText.text = keyboard
                ? "Hold <color=#FFC040>SPACE</color> to tilt the bottle.\nLet go to stop. Press <color=#FFC040>ENTER</color> to serve."
                : "<color=#FFC040>Click and drag right</color> to tilt the bottle.\nLet go to stop. Press <color=#FFC040>SERVE</color> when it looks right.";
        }

        void LateUpdate() => Tick();

        public void Tick() {
            if (!root.gameObject.activeInHierarchy) return;
            board.Tick();
            bubble.Tick();
        }
    }
}