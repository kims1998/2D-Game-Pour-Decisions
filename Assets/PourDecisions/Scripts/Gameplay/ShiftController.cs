using System.Collections;
using UnityEngine;

namespace PourDecisions {
    // End-of-shift numbers for the results screen.
    public class ShiftSummary {
        public GameMode mode;
        public int level;
        public int score, served, perfects, bestStreak, stars;
        public float tips, averageAccuracy;
        public bool passed, newBest, hasNextLevel;
    }

    /*
    * Runs a shift: deals orders, reads the player's tilt, ticks the bottle, stream and glass in a fixed
    * order every frame, grades each pour, and keeps score. One instance lives for the whole game and
    * is started and stopped by GameRoot.
    */
    public class ShiftController : MonoBehaviour {
        Stage stage;
        PourStream stream;
        Customer customer;
        Hud hud;

        Glass glass;
        Bottle bottle;
        Order order;

        GameMode mode;
        int levelIndex;
        LevelDef level;
        bool running;
        Coroutine routine;

        readonly TiltControl tiltControl = new TiltControl();
        bool serveRequested, skipRequested;

        // timers and score
        float orderTime, orderTimeLeft;
        int lastTickSecond = -1;
        int score, streak, bestStreak, perfects, served;
        float tips, accuracySum;
        int ordersDone;

        public bool Running => running;
        public GameMode Mode => mode;

        public void Init(Stage s, PourStream ps, Customer c, Hud h) {
            stage = s; stream = ps; customer = c; hud = h;
            hud.ServeClicked += () => RequestServe();
            hud.SkipClicked += () => skipRequested = true;
        }

        // ------------------------------------------------------------ lifecycle

        public void Begin(GameMode m, int levelIdx) {
            Stop();
            mode = m;
            levelIndex = levelIdx;
            level = m == GameMode.Classic ? GameData.Levels[levelIdx] : null;
            // Reset all the counters for a new shift.
            score = streak = bestStreak = perfects = served = ordersDone = 0;
            tips = accuracySum = 0f;
            running = true;

            Color ambient = stage.SetDim(level != null ? level.dim : 0f);
            stream.SetAmbient(ambient);
            customer.SetAmbient(ambient);
            customer.HideImmediate();
            Fx.I.ClearAll();

            string title = m == GameMode.Classic ? level.name : "Training";
            hud.BeginShift(m, title, level != null ? level.orders : 0);
            hud.SetScore(0);
            hud.SetTips(0f, false);
            hud.SetStreak(0);

            // A coroutine is a function that can pause (yield) and continue on a later frame.
            // ShiftLoop uses one so a whole shift reads like a simple top-to-bottom script.
            routine = StartCoroutine(ShiftLoop());
        }

        // Tears down the current shift without showing results.
        public void Stop() {
            running = false;
            if (routine != null) StopCoroutine(routine);
            routine = null;
            StopAllCoroutines();
            // Remove the leftover glass and bottle from the previous order, if any.
            if (glass != null) Destroy(glass.gameObject);
            if (bottle != null) Destroy(bottle.gameObject);
            glass = null;
            bottle = null;
            order = null;
            stream.Target = null;
            stream.ResetOrder();
            stage.ResetCamera();
            stage.SetDim(0f);
            customer.HideImmediate();
            AudioManager.I.StopPour();
            tiltControl.Reset();
        }

        public void RequestServe() {
            if (glass != null && glass.CurrentMl > 0.5f) serveRequested = true;
        }

        // Classic ends after the level's orders; Training runs until the player quits.
        bool ShiftOver => mode == GameMode.Classic && ordersDone >= level.orders;

        IEnumerator ShiftLoop() {
            hud.Banner(hud.TitleText, mode == GameMode.Classic ? level.blurb : "No clock. Target line shown. Tab to skip.", 1.8f);
            yield return new WaitForSeconds(1.2f);

            while (!ShiftOver) {
                // One order = deal it, let the player pour, grade it, then clear the counter.
                yield return DealOrder();
                yield return PourPhase();

                if (skipRequested) {
                    skipRequested = false;
                    yield return ClearOrder(false);
                    continue;
                }

                yield return Serve();
                yield return ClearOrder(true);
            }

            Finish();
        }

        // ------------------------------------------------------------ order flow

        IEnumerator DealOrder() {
            orderTime = mode == GameMode.Classic ? level.secondsPerOrder : 0f;
            order = OrderGenerator.Make(level, orderTime, order);
            orderTimeLeft = orderTime;
            lastTickSecond = -1;
            serveRequested = false;
            skipRequested = false;
            tiltControl.Reset();

            // glass slides in from the customer's side
            // Glasses and bottles are created from code for each order, then destroyed when it is done.
            var gGo = new GameObject("Glass_" + order.glass.id);
            glass = gGo.AddComponent<Glass>();
            glass.Init(order.glass);
            glass.SetLiquid(order.liquid);
            glass.SetAmbient(stage.Ambient);
            gGo.transform.position = new Vector3(11f, K.CounterY, 0f);
            stream.Target = glass;
            stream.ResetOrder();
            stage.FocusGlass = glass;

            // fresh bottle with this order's spout
            var bGo = new GameObject("Bottle_" + order.liquid.id);
            bottle = bGo.AddComponent<Bottle>();
            var bDef = Art.Bottle(order.liquid.bottleId);
            bottle.Init(bDef, order.liquid, order.spout, order.bottleFill, order.flowMultiplier);
            bottle.SetAmbient(stage.Ambient);
            // rest is where the bottle stands on the counter: left of the glass, base on the counter surface.
            Vector2 rest = new Vector2(K.GlassX - 4.3f, K.CounterY - bDef.bodyBottomY * K.BottleScale);
            bottle.RestPivot = new Vector2(-12f, rest.y);
            bottle.Tick(0f, 0f, null, stream);

            customer.Setup(order.customer);
            hud.SetOrder(order, ordersDone, mode);
            hud.SetTimer(1f, orderTime);
            hud.HideBubble();

            StartCoroutine(customer.Enter());
            AudioManager.I.PlayVaried("slide", 0.7f);
            StartCoroutine(Tween.Move(gGo.transform, new Vector3(K.GlassX, K.CounterY, 0f), 0.55f, Tween.EaseOutCubic));

            yield return Tween.Run(0.55f, t => {
                if (bottle == null) return;
                bottle.RestPivot = Vector2.Lerp(new Vector2(-12f, rest.y), rest, Tween.EaseOutCubic(t));
                bottle.Tick(0f, 0f, null, stream);
            });

            AudioManager.I.PlayVaried("clink", 0.6f);
            AudioManager.I.PlayVaried("cork_pop", 0.55f);
            yield return new WaitForSeconds(0.15f);
            hud.ShowBubble(order.request, customer);
            if (mode == GameMode.Training) glass.ShowTarget(order.targetMl, true, new Color(1f, 0.9f, 0.3f, 0.9f));
            hud.ShowHint(!Settings.HintSeen, Settings.HardMode);
        }

        IEnumerator PourPhase() {
            bool lifted = false;

            while (true) {
                float dt = Time.deltaTime;
                if (GameRoot.Paused) { yield return null; continue; }

                // forced means the order is ending (served, skipped or out of time): ignore input, bring the bottle up.
                bool forced = serveRequested || skipRequested;

                // clocks
                if (mode == GameMode.Classic && !forced) {
                    orderTimeLeft -= dt;
                    TickWarning(orderTimeLeft);
                    if (orderTimeLeft <= 0f) { orderTimeLeft = 0f; forced = serveRequested = true; hud.Flash("Time's up!"); }
                    hud.SetTimer(orderTimeLeft / orderTime, orderTimeLeft);
                }

                // Every frame, in this order: read input, move the bottle, move the liquid in flight, update the HUD.
                tiltControl.Update(dt, forced);

                if (!lifted && tiltControl.Tilt > 8f) {
                    // The order is on the ticket too, so tuck the bubble away and keep the glass clear.
                    lifted = true;
                    AudioManager.I.PlayVaried("bottle_lift", 0.5f);
                    hud.HideBubble();
                }

                // The bottle works out the flow and emits liquid into the stream; the stream then moves it into the glass.
                bottle.Tick(tiltControl.Tilt, dt, glass, stream);
                stream.Tick(dt);

                // audio, camera and HUD feedback
                bool pouring = bottle.Flow > 0.1f || stream.Busy;
                AudioManager.I.UpdatePour(stream.LandingRate, glass.Fill01, order.liquid.thick);
                stage.Focus = Mathf.Clamp01((tiltControl.Tilt - 15f) / 35f);
                hud.SetServeEnabled(glass.CurrentMl > 0.5f && !forced);
                if (mode == GameMode.Training) hud.SetLive(glass.CurrentMl, order.targetMl, bottle.Flow);
                if (glass.CurrentMl > 0.5f && !Settings.HintSeen && !pouring) { Settings.HintSeen = true; hud.ShowHint(false, false); }

                if (PourInput.ServePressed) RequestServe();
                if (mode == GameMode.Training && PourInput.SkipPressed) skipRequested = true;

                // A serve waits until the bottle is back up and every drop has landed.
                if ((serveRequested || skipRequested) && tiltControl.Tilt < 2f && bottle.Flow <= 0f && !stream.Busy) break;
                yield return null;
            }

            AudioManager.I.StopPour();
            stage.Focus = 0f;
            hud.SetServeEnabled(false);
        }

        void TickWarning(float secondsLeft) {
            int s = Mathf.CeilToInt(secondsLeft);

            if (s <= 3 && s >= 1 && s != lastTickSecond) {
                lastTickSecond = s;
                AudioManager.I.Play(s == 1 ? "tick_urgent" : "tick", 0.7f);
            }
        }

        IEnumerator Serve() {
            bool hard = Settings.HardMode;
            float timeLeft01 = mode == GameMode.Classic && orderTime > 0f ? orderTimeLeft / orderTime : 0f;
            float spilled = stream.SpilledMl + glass.OverflowMl;
            // Grade the pour with the rules in PourMath.cs, then update score, streak, tips and the screen.
            var r = Scoring.Evaluate(glass.CurrentMl, order.targetMl, spilled, glass.Overflowed, timeLeft01, streak, hard);

            served++;
            accuracySum += r.accuracy;
            score += r.points;
            tips += r.tips;
            if (r.grade == Grade.Perfect) perfects++;
            streak = Scoring.KeepsStreak(r.grade) ? streak + 1 : 0;
            bestStreak = Mathf.Max(bestStreak, streak);

            // reveal where the line should have been
            glass.ShowTarget(order.targetMl, true, new Color(1f, 0.9f, 0.3f, 0.95f));
            customer.SetMood(Customer.MoodFor(r.grade));
            hud.ShowBubble(OrderGenerator.Reaction(r.grade), customer);
            hud.ShowGrade(r);
            hud.SetScore(score);
            hud.SetStreak(streak);
            PlayGradeSound(r);

            if (r.tips > 0f) {
                hud.SetTips(tips, true);
                AudioManager.I.Play("register", 0.55f);
            }

            if (r.grade == Grade.Perfect)
                Fx.I.Sparkles(new Vector2(glass.CenterX, glass.SurfaceY + 0.3f), 12, new Color(1f, 0.92f, 0.5f));
            yield return new WaitForSeconds(1.7f);
        }

        void PlayGradeSound(PourResult r) {
            var a = AudioManager.I;

            switch (r.grade) {
                case Grade.Perfect:
                    a.Play("grade_perfect", 0.8f);
                    break;
                case Grade.Great:
                    a.Play("grade_great", 0.75f);
                    break;
                case Grade.Good:
                    a.Play("grade_good", 0.7f);
                    break;
                case Grade.Spilled:
                    a.Play("grade_spilled", 0.7f);
                    break;
                default:
                    a.Play("grade_miss", 0.6f);
                    break;
            }
        }

        // Glass slides off to the customer (or away, when skipped) and the customer leaves.
        IEnumerator ClearOrder(bool countIt) {
            if (countIt) ordersDone++;
            hud.HideBubble();
            hud.SetLive(-1f, 0f, 0f);
            var oldGlass = glass;
            var oldBottle = bottle;
            AudioManager.I.PlayVaried("slide", 0.6f);
            StartCoroutine(customer.Leave());
            if (oldBottle != null)
                StartCoroutine(Tween.Run(0.45f, t => {
                    if (oldBottle == null) return;
                    oldBottle.RestPivot = Vector2.Lerp(oldBottle.RestPivot, new Vector2(-12f, oldBottle.RestPivot.y), Tween.EaseInCubic(t));
                    oldBottle.Tick(0f, 0f, null, stream);
                }));
            if (oldGlass != null)
                yield return Tween.Move(oldGlass.transform, new Vector3(11.5f, K.CounterY, 0f), 0.5f, Tween.EaseInCubic);
            if (oldGlass != null) Destroy(oldGlass.gameObject);
            if (oldBottle != null) Destroy(oldBottle.gameObject);
            if (glass == oldGlass) glass = null;
            if (bottle == oldBottle) bottle = null;
            stream.Target = null;
            stream.ResetOrder();
            stage.FocusGlass = null;
        }

        void Finish() {
            running = false;
            var s = new ShiftSummary {
                mode = mode, level = levelIndex, score = score, served = served,
                perfects = perfects, bestStreak = bestStreak, tips = tips,
                averageAccuracy = served > 0 ? accuracySum / served : 0f,
            };

            if (mode == GameMode.Classic) {
                // Stars depend on the share of the maximum possible points earned.
                float ratio = score / (float)(level.orders * Scoring.MaxBasePoints);
                s.stars = ratio >= 0.85f ? 3 : ratio >= 0.65f ? 2 : ratio >= 0.4f ? 1 : 0;
                s.passed = s.stars >= 1;
                Settings.RecordStars(levelIndex, s.stars);
                if (s.passed) Settings.Unlock(levelIndex + 1);
                s.hasNextLevel = s.passed && levelIndex + 1 < GameData.Levels.Length;
            } else s.passed = true;
            s.newBest = mode != GameMode.Training && Settings.RecordBest(mode, levelIndex, score);
            AudioManager.I.Play(s.passed ? "level_complete" : "level_fail", 0.8f);
            GameRoot.I.ShowResults(s);
        }
    }
}