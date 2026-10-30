using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PourDecisions {
    // Title screen: pick a mode, then controls and sensitivity options.
    public class TitleScreen {
        public RectTransform Root { get; private set; }
        Text controlsLabel, sensLabel, bestText, blurb;

        public void Build(RectTransform canvas) {
            Root = UIKit.Rect("Title", canvas).Stretch();
            UIKit.Blocker(Root, new Color(0.05f, 0.03f, 0.03f, 0.45f));

            var logo = UIKit.Image(Root, "Logo", Art.Sprite("logo"), Color.white);
            logo.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(1000f, 323f));
            blurb = UIKit.Label(Root, "", Art.Body, 32, Palette.Cream).Shadowed(2f, 0.8f);
            blurb.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -330f), new Vector2(1200f, 50f));

            var col = UIKit.Rect("Modes", Root);
            col.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(520f, 420f));
            AddMode(col, 0, "CLASSIC", Palette.Amber, "Five shifts, each tougher than the last", () => GameRoot.I.ShowLevelSelect());
            AddMode(col, 1, "TRAINING", Palette.Teal, "No clock, target line and live readout", () => GameRoot.I.StartGame(GameMode.Training, 0));

            bestText = UIKit.Label(Root, "", Art.Body, 26, new Color(1f, 1f, 1f, 0.8f)).Shadowed(2f, 0.8f);
            bestText.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(1000f, 40f));

            // options row along the bottom
            var opts = UIKit.Rect("Options", Root);
            opts.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(1700f, 90f));
            Option(opts, -180f, 420f, Palette.Slate, () => { Settings.HardMode = !Settings.HardMode; Refresh(); }, out controlsLabel);
            Option(opts, 225f, 330f, Palette.Slate, () => { Settings.Sensitivity = Settings.Sensitivity >= 1.99f ? 0.5f : Settings.Sensitivity + 0.25f; Refresh(); }, out sensLabel);

            var quit = UIKit.Button(Root, "QUIT", new Color(0.35f, 0.2f, 0.2f), GameRoot.Quit, 30);
            ((RectTransform)quit.transform).Place(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -24f), new Vector2(150f, 70f));
            Refresh();
        }

        static void AddMode(RectTransform col, int index, string label, Color color, string tip, System.Action action) {
            var b = UIKit.Button(col, label, color, action, 52);
            ((RectTransform)b.transform).Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -index * 140f), new Vector2(460f, 100f));
            var t = UIKit.Label(col, tip, Art.Body, 22, Palette.Cream).Shadowed(2f, 0.9f);
            t.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -index * 140f - 104f), new Vector2(700f, 30f));
        }

        static Button Option(RectTransform parent, float x, float width, Color color, System.Action action, out Text label) {
            var b = UIKit.Button(parent, "", color, action, 28);
            ((RectTransform)b.transform).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, 0f), new Vector2(width, 80f));
            label = UIKit.LabelOf(b);
            label.font = Art.BodyHeavy;
            label.fontSize = 24;
            
            return b;
        }

        public void Refresh() {
            controlsLabel.text = Settings.HardMode ? "Controls: KEYBOARD  (hard, +25%)" : "Controls: MOUSE  (keyboard works too)";
            sensLabel.text = $"Mouse sensitivity: {Settings.Sensitivity:0.00}x";
            blurb.text = "Pour the perfect shot by eye. No jiggers allowed.";
            int stars = 0;
            for (int i = 0; i < GameData.Levels.Length; i++) stars += Settings.Stars(i);
            bestText.text = $"Classic stars: {stars} / {GameData.Levels.Length * 3}";
        }
    }

    // Classic mode level picker with stars and locks.</summary>
    public class LevelSelectScreen {
        public RectTransform Root { get; private set; }
        readonly List<Button> cards = new List<Button>();
        readonly List<Text> names = new List<Text>(), details = new List<Text>();
        readonly List<Image[]> stars = new List<Image[]>();

        public void Build(RectTransform canvas) {
            Root = UIKit.Rect("LevelSelect", canvas).Stretch();
            UIKit.Blocker(Root, new Color(0.05f, 0.03f, 0.03f, 0.7f));
            var title = UIKit.Label(Root, "Choose a Shift", Art.Display, 80, Palette.Amber).Shadowed(4f, 0.7f);
            title.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(1200f, 100f));

            int n = GameData.Levels.Length;

            for (int i = 0; i < n; i++) {
                int index = i;
                var b = UIKit.Button(Root, "", Palette.Slate, () => GameRoot.I.StartGame(GameMode.Classic, index), 30);
                var rt = (RectTransform)b.transform;
                float x = (i - (n - 1) / 2f) * 340f;
                rt.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, 20f), new Vector2(310f, 420f));
                var num = UIKit.Label(rt, (i + 1).ToString(), Art.Display, 110, new Color(1f, 1f, 1f, 0.18f));
                num.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(300f, 130f));
                var name = UIKit.Label(rt, "", Art.Display, 38, Palette.Cream);
                name.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(290f, 100f));
                var det = UIKit.Label(rt, "", Art.Body, 22, new Color(1f, 1f, 1f, 0.85f));
                det.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -220f), new Vector2(280f, 110f));
                var row = new Image[3];

                for (int s = 0; s < 3; s++) {
                    row[s] = UIKit.Image(rt, "Star", Art.Sprite("star_empty"), Color.white);
                    row[s].rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2((s - 1) * 70f, 24f), new Vector2(64f, 64f));
                }

                cards.Add(b); names.Add(name); details.Add(det); stars.Add(row);
            }

            var back = UIKit.Button(Root, "BACK", Palette.Slate, () => GameRoot.I.ShowTitle(), 36);
            ((RectTransform)back.transform).Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(260f, 84f));
        }

        public void Refresh() {
            int unlocked = Settings.UnlockedLevel();
            for (int i = 0; i < cards.Count; i++) {
                var lv = GameData.Levels[i];
                bool open = i <= unlocked;
                cards[i].interactable = open;
                ((Image)cards[i].targetGraphic).color = open ? (i == unlocked ? Palette.Teal : Palette.Slate) : new Color(0.2f, 0.2f, 0.22f);
                names[i].text = open ? lv.name : "Locked";
                details[i].text = open
                    ? $"{lv.blurb}\n<size=19>{lv.orders} orders  •  {lv.secondsPerOrder:0}s each</size>"
                    : "Earn a star on the previous shift to unlock.";
                int got = Settings.Stars(i);
                for (int s = 0; s < 3; s++) stars[i][s].sprite = Art.Sprite(s < got ? "star_full" : "star_empty");
            }
        }
    }

    // Pause overlay (Esc).
    public class PauseMenu {
        public RectTransform Root { get; private set; }

        public void Build(RectTransform canvas) {
            Root = UIKit.Rect("Pause", canvas).Stretch();
            UIKit.Blocker(Root, Palette.Dim);
            var panel = UIKit.Panel(Root, "Panel", Palette.Panel);
            panel.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(560f, 560f));
            var t = UIKit.Label(panel.transform, "Paused", Art.Display, 80, Palette.Amber);
            t.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(500f, 100f));
            string[] labels = { "RESUME", "RESTART", "MAIN MENU" };
            System.Action[] actions = { () => GameRoot.I.TogglePause(), () => GameRoot.I.Restart(), () => GameRoot.I.ShowTitle() };
            Color[] colors = { Palette.Green, Palette.Amber, Palette.Slate };
            
            for (int i = 0; i < labels.Length; i++) {
                var b = UIKit.Button(panel.transform, labels[i], colors[i], actions[i], 42);
                ((RectTransform)b.transform).Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -160f - i * 120f), new Vector2(400f, 96f));
            }
        }
    }

    // End of shift: score, stars, stats and what to do next.
    public class ResultsScreen {
        public RectTransform Root { get; private set; }
        Text title, scoreText, stats, record;
        Image[] stars;
        Button next, retry, menu;

        public void Build(RectTransform canvas) {
            Root = UIKit.Rect("Results", canvas).Stretch();
            UIKit.Blocker(Root, Palette.Dim);
            var panel = UIKit.Panel(Root, "Panel", Palette.Panel);
            panel.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 820f));
            var p = panel.transform;
            title = UIKit.Label(p, "", Art.Display, 84, Palette.Amber);
            title.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(860f, 100f));
            stars = new Image[3];

            for (int i = 0; i < 3; i++) {
                stars[i] = UIKit.Image(p, "Star", Art.Sprite("star_empty"), Color.white);
                stars[i].rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2((i - 1) * 130f, -140f - (i == 1 ? 0f : 20f)), new Vector2(120f, 120f));
            }
            
            scoreText = UIKit.Label(p, "", Art.Display, 96, Color.white).Shadowed(3f, 0.6f);
            scoreText.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -280f), new Vector2(860f, 110f));
            record = UIKit.Label(p, "NEW BEST!", Art.Display, 38, Palette.Green);
            record.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -380f), new Vector2(860f, 50f));
            stats = UIKit.Label(p, "", Art.Body, 32, Palette.Cream);
            stats.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -430f), new Vector2(800f, 200f));
            stats.alignment = TextAnchor.UpperCenter;

            next = UIKit.Button(p, "NEXT SHIFT", Palette.Green, () => GameRoot.I.NextLevel(), 36);
            ((RectTransform)next.transform).Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-280f, 40f), new Vector2(260f, 90f));
            retry = UIKit.Button(p, "RETRY", Palette.Amber, () => GameRoot.I.Restart(), 36);
            ((RectTransform)retry.transform).Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(260f, 90f));
            menu = UIKit.Button(p, "MENU", Palette.Slate, () => GameRoot.I.ShowTitle(), 36);
            ((RectTransform)menu.transform).Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(280f, 40f), new Vector2(260f, 90f));
        }

        public void Show(ShiftSummary s) {
            bool classic = s.mode == GameMode.Classic;
            title.text = classic ? (s.passed ? "Shift Complete!" : "Rough Shift...") : "Training Over";
            foreach (var st in stars) st.gameObject.SetActive(classic);
            for (int i = 0; i < 3; i++) stars[i].sprite = Art.Sprite(i < s.stars ? "star_full" : "star_empty");
            scoreText.text = s.score.ToString();
            record.gameObject.SetActive(s.newBest);
            stats.text = $"Drinks served: {s.served}     Perfect pours: {s.perfects}\n" +
                         $"Average accuracy: {s.averageAccuracy:0.0}%     Best streak: {s.bestStreak}\n" +
                         $"Tips: ${s.tips:0.00}" +
                         (classic && !s.passed ? "\n<color=#FF9A80>Earn at least one star to unlock the next shift.</color>" : "");
            next.gameObject.SetActive(classic && s.hasNextLevel);
            var r = (RectTransform)retry.transform;
            r.anchoredPosition = new Vector2(next.gameObject.activeSelf ? 0f : -150f, 40f);
            ((RectTransform)menu.transform).anchoredPosition = new Vector2(next.gameObject.activeSelf ? 280f : 150f, 40f);
        }
    }
}