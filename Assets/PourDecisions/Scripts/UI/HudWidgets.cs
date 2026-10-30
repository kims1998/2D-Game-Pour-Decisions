using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PourDecisions {
    /*
    * Base for the pieces of the HUD. Widgets are plain classes that build their own UI and borrow a
    * MonoBehaviour (the Hud) to run animations, so each one stays small and self-contained.
    */
    public abstract class HudWidget {
        protected readonly MonoBehaviour host;

        protected HudWidget(MonoBehaviour host) => this.host = host;

        protected Coroutine Run(IEnumerator routine) => host.StartCoroutine(routine);

        protected void Stop(Coroutine routine) {
            if (routine != null) host.StopCoroutine(routine);
        }

        // Quick overshoot "pop" when something appears or changes.
        protected static IEnumerator Pop(RectTransform rt, float duration) {
            yield return Tween.Run(duration, t => {
                if (rt != null) rt.localScale = Vector3.one * Mathf.LerpUnclamped(0.85f, 1f, Tween.EaseOutBack(t));
            });
        }
    }

    // The order ticket, with the per-order timer at the bottom.
    public class OrderTicket : HudWidget {
        readonly Image ticket, swatch, timerFill;
        readonly Text header, amount, liquid, glass, ml, spout, timerText;
        readonly RectTransform timerGroup;

        public OrderTicket(MonoBehaviour host, RectTransform root) : base(host) {
            ticket = UIKit.Image(root, "Ticket", Art.Sprite("ui_ticket"), Color.white);
            ticket.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -10f), new Vector2(360f, 468f));
            var t = ticket.transform;
            header = Line(t, Art.Display, 30, Color.white, -45f, 40f);
            amount = Line(t, Art.Display, 54, Palette.Ink, -118f, 64f);
            swatch = UIKit.Image(t, "Swatch", Art.Sprite("fx_droplet"), Color.white);
            swatch.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(44f, -176f), new Vector2(40f, 40f));
            liquid = UIKit.Label(t, "", Art.BodyHeavy, 36, Palette.Ink, TextAnchor.MiddleLeft);
            liquid.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(72f, -176f), new Vector2(280f, 48f));
            glass = Line(t, Art.Body, 28, new Color(0.3f, 0.25f, 0.22f), -226f, 40f);
            ml = Line(t, Art.Body, 24, new Color(0.45f, 0.4f, 0.36f), -262f, 34f);
            spout = Line(t, Art.Body, 26, new Color(0.55f, 0.2f, 0.15f), -306f, 40f);

            timerGroup = UIKit.Rect("Timer", t);
            timerGroup.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 62f), new Vector2(320f, 50f));
            var clock = UIKit.Image(timerGroup, "Clock", Art.Sprite("icon_clock"), Color.white);
            clock.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(40f, 40f));
            var barBg = UIKit.Image(timerGroup, "BarBg", Art.Sliced("ui_panel", 30f), new Color(0.2f, 0.15f, 0.12f, 0.35f), true);
            barBg.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(50f, 0f), new Vector2(190f, 22f));
            timerFill = UIKit.Image(barBg.transform, "Fill", Art.Sprite("fx_white"), Palette.Green);
            timerFill.preserveAspect = false;
            timerFill.type = Image.Type.Filled;
            timerFill.fillMethod = Image.FillMethod.Horizontal;
            timerFill.rectTransform.Stretch(3f);
            timerText = UIKit.Label(timerGroup, "", Art.BodyHeavy, 26, Palette.Ink, TextAnchor.MiddleRight);
            timerText.rectTransform.Place(new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(80f, 40f));
        }

        // A centered line of text at a fixed height on the ticket.
        static Text Line(Transform parent, Font font, int size, Color color, float y, float height) {
            var text = UIKit.Label(parent, "", font, size, color);
            text.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(330f, height));

            return text;
        }

        public void Begin(GameMode mode) {
            timerGroup.gameObject.SetActive(mode != GameMode.Training);
        }

        public void Show(Order o, int index, int total, GameMode mode) {
            header.text = mode == GameMode.Classic ? $"ORDER {index + 1} / {total}" : $"ORDER #{index + 1}";
            amount.text = GameData.AmountText(o.shots);
            liquid.text = o.liquid.name;
            var c = o.liquid.color;
            swatch.color = new Color(c.r, c.g, c.b, 1f);
            glass.text = "in the " + o.glass.name;
            ml.text = mode == GameMode.Training ? $"({o.targetMl:0.0} ml, glass holds {o.glass.capacityMl:0} ml)" : "";
            spout.text = "Spout: " + o.spout.name;
            Run(Pop(ticket.rectTransform, 0.3f));
        }

        public void SetTimer(float fraction, float seconds) {
            fraction = Mathf.Clamp01(fraction);
            timerFill.fillAmount = fraction;
            timerFill.color = fraction > 0.5f ? Palette.Green : fraction > 0.25f ? Palette.Amber : Palette.Red;
            timerText.text = seconds > 0f ? (seconds < 10f ? seconds.ToString("0.0") : seconds.ToString("0")) + "s" : "0s";
        }
    }

    // Score panel with a counting-up number, the streak flame, and the tip jar that fills with coins.
    public class ScoreBoard : HudWidget {
        const int MaxCoins = 36;
        const float DollarsPerCoin = 0.5f;

        readonly Text scoreText, streakText, tipsText;
        readonly Image flame;
        readonly RectTransform jar, coinLayer;
        readonly List<Image> coins = new List<Image>();
        int shownScore, targetScore;

        public ScoreBoard(MonoBehaviour host, RectTransform root) : base(host) {
            var panel = UIKit.Panel(root, "ScorePanel", Palette.Panel);
            panel.raycastTarget = false;
            panel.rectTransform.Place(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -16f), new Vector2(300f, 140f));
            var label = UIKit.Label(panel.transform, "SCORE", Art.Body, 24, new Color(1f, 1f, 1f, 0.7f));
            label.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(280f, 30f));
            scoreText = UIKit.Label(panel.transform, "0", Art.Display, 62, Palette.Amber);
            scoreText.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(280f, 70f));
            flame = UIKit.Image(panel.transform, "Flame", Art.Sprite("icon_flame"), Color.white);
            flame.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(1f, 0.5f), new Vector2(-6f, 22f), new Vector2(30f, 30f));
            streakText = UIKit.Label(panel.transform, "", Art.BodyHeavy, 24, Palette.Cream, TextAnchor.MiddleLeft);
            streakText.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0f, 0.5f), new Vector2(0f, 22f), new Vector2(150f, 30f));

            jar = UIKit.Rect("TipJar", root);
            jar.Place(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-98f, -168f), new Vector2(154f, 196f));
            UIKit.Image(jar, "Back", Art.Sprite("tipjar_back"), Color.white).rectTransform.Stretch();
            coinLayer = UIKit.Rect("Coins", jar).Stretch();
            UIKit.Image(jar, "Front", Art.Sprite("tipjar_front"), Color.white).rectTransform.Stretch();
            tipsText = UIKit.Label(jar, "$0.00", Art.Display, 34, Palette.Amber).Shadowed(2f, 0.8f);
            tipsText.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -2f), new Vector2(200f, 44f));
        }

        public void Reset() {
            foreach (var c in coins) Object.Destroy(c.gameObject);
            coins.Clear();
            shownScore = targetScore = 0;
            scoreText.text = "0";
            tipsText.text = "$0.00";
            SetStreak(0);
        }

        public void SetScore(int score) => targetScore = score;

        public void SetStreak(int streak) {
            flame.gameObject.SetActive(streak >= 2);
            streakText.text = streak >= 2 ? $"x{streak} streak" : "";
        }

        public void SetTips(float total, bool animate) {
            tipsText.text = "$" + total.ToString("0.00");
            int want = Mathf.Min(MaxCoins, Mathf.FloorToInt(total / DollarsPerCoin));
            int firstNew = coins.Count;
            
            while (coins.Count < want) {
                var coin = AddCoin(coins.Count, out float restY);
                // new coins drop one after another
                if (animate) Run(DropCoin(coin.rectTransform, restY, coins.Count - 1 - firstNew));
            }

            if (animate) Run(Pop(jar, 0.25f));
        }

        // Places coin number i in loose rows from the bottom of the jar.
        Image AddCoin(int i, out float y) {
            var coin = UIKit.Image(coinLayer, "Coin", Art.Sprite("coin"), Color.white);
            float x = 34f + (i * 37 % 86) + Random.Range(-6f, 6f);
            y = 30f + (i / 3) * 12f + Random.Range(-3f, 3f);
            coin.rectTransform.Place(Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(x, y), new Vector2(30f, 30f));
            coin.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-40f, 40f));
            coins.Add(coin);

            return coin;
        }

        IEnumerator DropCoin(RectTransform coin, float restY, int delaySlots) {
            const float startY = 240f;
            var p = coin.anchoredPosition;
            coin.anchoredPosition = new Vector2(p.x, startY);
            yield return new WaitForSeconds(0.08f * delaySlots);

            yield return Tween.Run(0.25f, t => {
                if (coin != null) coin.anchoredPosition = new Vector2(p.x, Mathf.Lerp(startY, restY, Tween.EaseInCubic(t)));
            });

            AudioManager.I.PlayVaried("coin", 0.35f, 0.15f);
        }

        // Counts the displayed score up toward the real one.
        public void Tick() {
            if (shownScore == targetScore) return;
            int step = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(targetScore - shownScore) * 8f * Time.unscaledDeltaTime));
            shownScore = (int)Mathf.MoveTowards(shownScore, targetScore, step);
            scoreText.text = shownScore.ToString();
        }
    }

    // The customer's speech bubble. Its tail follows the customer's head as the camera moves.
    public class SpeechBubble : HudWidget {
        readonly RectTransform root, bubble;
        readonly Text text;
        readonly Camera cam;
        Customer target;

        public SpeechBubble(MonoBehaviour host, RectTransform root, Camera cam) : base(host) {
            this.root = root;
            this.cam = cam;
            bubble = UIKit.Image(root, "Bubble", Art.Sprite("ui_bubble"), Color.white).rectTransform;
            bubble.anchorMin = bubble.anchorMax = new Vector2(0.5f, 0.5f);
            bubble.sizeDelta = new Vector2(420f, 210f);
            // pivot sits on the tip of the tail in the bubble art
            bubble.pivot = new Vector2(470f / 520f, 8f / 260f);
            text = UIKit.Label(bubble, "", Art.BodyHeavy, 28, Palette.Ink);
            text.rectTransform.anchorMin = new Vector2(0.05f, 0.28f);
            text.rectTransform.anchorMax = new Vector2(0.95f, 0.95f);
            text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
            Hide();
        }

        public void Show(string message, Customer who) {
            target = who;
            text.text = message;
            bubble.gameObject.SetActive(true);
            Tick();
            Run(Pop(bubble, 0.25f));
        }

        public void Hide() {
            target = null;
            bubble.gameObject.SetActive(false);
        }

        public void Tick() {
            if (target == null || cam == null || !bubble.gameObject.activeSelf) return;
            Vector2 world = target.HeadWorld + new Vector2(-0.8f, 0.75f);
            Vector2 screen = cam.WorldToScreenPoint(new Vector3(world.x, world.y, 0f));
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, null, out var local)) return;
            float halfW = root.rect.width * 0.5f, halfH = root.rect.height * 0.5f;
            local.x = Mathf.Clamp(local.x, -halfW + bubble.sizeDelta.x * 0.92f, halfW - 30f);
            local.y = Mathf.Clamp(local.y, -halfH + 20f, halfH - bubble.sizeDelta.y - 140f);
            bubble.anchoredPosition = local;
        }
    }

    // The big "PERFECT!" popup with the measured amount and points earned.
    public class GradePopup : HudWidget {
        readonly RectTransform group;
        readonly CanvasGroup fade;
        readonly Text label, detail, points;
        Coroutine routine;

        public GradePopup(MonoBehaviour host, RectTransform root) : base(host) {
            group = UIKit.Rect("Grade", root);
            group.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-120f, 230f), new Vector2(1000f, 300f));
            fade = group.gameObject.AddComponent<CanvasGroup>();
            fade.blocksRaycasts = false;
            var dark = new Color(0.15f, 0.08f, 0.05f);
            label = UIKit.Label(group, "", Art.Display, 120, Color.white).Outlined(dark, 5f).Shadowed(6f, 0.5f);
            label.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(1000f, 140f));
            detail = UIKit.Label(group, "", Art.BodyHeavy, 34, Color.white).Outlined(new Color(0f, 0f, 0f, 0.8f), 2f);
            detail.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(1000f, 50f));
            points = UIKit.Label(group, "", Art.Display, 44, Palette.Amber).Outlined(dark, 3f);
            points.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -196f), new Vector2(1000f, 60f));
            Hide();
        }

        public void Hide() => group.gameObject.SetActive(false);

        public void Show(PourResult r) {
            Stop(routine);
            group.gameObject.SetActive(true);
            label.text = Scoring.Label(r.grade);
            label.color = Scoring.ColorOf(r.grade);
            detail.text = DetailText(r);
            string pts = r.points > 0 ? $"+{r.points} pts" : "";
            if (r.tips > 0f) pts += $"    +${r.tips:0.00} tip";
            points.text = pts;
            routine = Run(Animate());
        }

        // "44.1 ml of 44.4 ml (−0.3 ml) • 99% accurate".
        static string DetailText(PourResult r) {
            if (r.grade == Grade.Empty) return "";
            string sign = r.errorMl >= 0 ? "+" : "−";
            string amounts = $"{r.pouredMl:0.0} ml of {r.targetMl:0.0} ml";
            string text = $"{amounts}   ({sign}{Mathf.Abs(r.errorMl):0.0} ml)   •   {r.accuracy:0}% accurate";
            if (r.spilledMl > 0.5f && r.grade != Grade.Spilled) text += $"   •   spilled {r.spilledMl:0.0} ml";

            return text;
        }

        IEnumerator Animate() {
            fade.alpha = 1f;
            yield return Tween.Run(0.35f, t => group.localScale = Vector3.one * Mathf.LerpUnclamped(0.4f, 1f, Tween.EaseOutBack(t)));
            yield return new WaitForSeconds(1.1f);
            yield return Tween.Run(0.3f, t => fade.alpha = 1f - t);
            group.gameObject.SetActive(false);
        }
    }

    // The shift-start banner and short warnings like "Time's up!".</summary>
    public class Announcer : HudWidget {
        readonly RectTransform banner;
        readonly CanvasGroup bannerFade;
        readonly Text bannerTitle, bannerSub, flash;
        Coroutine bannerRoutine, flashRoutine;

        public Announcer(MonoBehaviour host, RectTransform root) : base(host) {
            var panel = UIKit.Panel(root, "Banner", new Color(0.1f, 0.06f, 0.06f, 0.88f));
            panel.raycastTarget = false;
            banner = panel.rectTransform;
            banner.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(980f, 220f));
            bannerFade = banner.gameObject.AddComponent<CanvasGroup>();
            bannerFade.blocksRaycasts = false;
            bannerTitle = UIKit.Label(banner, "", Art.Display, 84, Palette.Amber).Shadowed(4f, 0.6f);
            bannerTitle.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(940f, 100f));
            bannerSub = UIKit.Label(banner, "", Art.Body, 32, Palette.Cream);
            bannerSub.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(920f, 70f));

            flash = UIKit.Label(root, "", Art.Display, 72, Palette.Red).Outlined(new Color(0.1f, 0f, 0f), 4f);
            flash.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 380f), new Vector2(900f, 100f));
            HideAll();
        }

        public void HideAll() {
            banner.gameObject.SetActive(false);
            flash.gameObject.SetActive(false);
        }

        public void Banner(string big, string small, float duration) {
            Stop(bannerRoutine);
            bannerTitle.text = big;
            bannerSub.text = small;
            bannerRoutine = Run(AnimateBanner(duration));
        }

        IEnumerator AnimateBanner(float duration) {
            banner.gameObject.SetActive(true);
            bannerFade.alpha = 0f;
            AudioManager.I.Play("whoosh", 0.5f);

            yield return Tween.Run(0.25f, t => {
                bannerFade.alpha = t;
                banner.localScale = Vector3.one * Mathf.Lerp(0.85f, 1f, Tween.EaseOutBack(t));
            });

            yield return new WaitForSeconds(duration);
            yield return Tween.Run(0.3f, t => bannerFade.alpha = 1f - t);
            banner.gameObject.SetActive(false);
        }

        public void Flash(string message) {
            Stop(flashRoutine);
            flash.text = message;
            flashRoutine = Run(AnimateFlash());
        }

        IEnumerator AnimateFlash() {
            flash.gameObject.SetActive(true);
            yield return Tween.Run(0.25f, t => flash.rectTransform.localScale = Vector3.one * Mathf.LerpUnclamped(1.6f, 1f, Tween.EaseOutBack(t)));
            yield return new WaitForSeconds(0.9f);
            flash.gameObject.SetActive(false);
        }
    }
}