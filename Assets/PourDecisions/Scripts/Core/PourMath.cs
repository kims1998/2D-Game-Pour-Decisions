using System.Collections.Generic;
using UnityEngine;

namespace PourDecisions
{
    /*
    * Volume math for a glass shaped like a truncated cone (frustum).
    * Everything is in centimeters, and 1 cm^3 = 1 ml, so the results are real milliliters.
    */
    public static class CupMath {
        // Interior radius at height h above the interior bottom.
        public static float RadiusAt(float rb, float rt, float H, float h) => rb + (rt - rb) * Mathf.Clamp01(h / H);

        // Volume of liquid filled to height h: V = pi*h/3 * (r0^2 + r0*r + r^2).
        public static float Volume(float rb, float rt, float H, float h) {
            h = Mathf.Clamp(h, 0f, H);
            float r = RadiusAt(rb, rt, H, h);

            return Mathf.PI * h / 3f * (rb * rb + rb * r + r * r);
        }

        // Inverse of Volume via bisection. Volume is monotonic in h, so 32 steps is exact to float precision.
        public static float HeightForVolume(float rb, float rt, float H, float ml) {
            if (ml <= 0f) return 0f;
            float lo = 0f, hi = H;

            for (int i = 0; i < 32; i++) {
                float mid = (lo + hi) * 0.5f;
                if (Volume(rb, rt, H, mid) < ml) lo = mid; else hi = mid;
            }

            return (lo + hi) * 0.5f;
        }
    }

    /*
    * Finds the liquid surface inside a tilted bottle.
    * The bottle interior is a few convex polygons in bottle-local space. For a fill fraction and a tilt,
    * we search for the horizontal water line whose "area below" equals that fraction of the interior.
    * The same water line tells us how far the spout sits below the surface (the "head"), which drives flow.
    */
    public static class BottleMath {
        // Clips a convex polygon to the half-plane dot(p, up) <= c (Sutherland-Hodgman, one edge).
        public static void ClipBelow(Vector2[] poly, Vector2 up, float c, List<Vector2> result) {
            result.Clear();
            int n = poly.Length;

            for (int i = 0; i < n; i++) {
                Vector2 a = poly[i];
                Vector2 b = poly[(i + 1) % n];
                float da = Vector2.Dot(a, up) - c;
                float db = Vector2.Dot(b, up) - c;
                if (da <= 0f) result.Add(a);

                if ((da < 0f && db > 0f) || (da > 0f && db < 0f)) {
                    float t = da / (da - db);
                    result.Add(a + (b - a) * t);
                }
            }
        }

        // Shoelace formula.
        public static float Area(List<Vector2> p) {
            float a = 0f;

            for (int i = 0; i < p.Count; i++) {
                Vector2 u = p[i], v = p[(i + 1) % p.Count];
                a += u.x * v.y - v.x * u.y;
            }

            return Mathf.Abs(a) * 0.5f;
        }

        public static float Area(Vector2[] p) {
            float a = 0f;

            for (int i = 0; i < p.Length; i++) {
                Vector2 u = p[i], v = p[(i + 1) % p.Length];
                a += u.x * v.y - v.x * u.y;
            }

            return Mathf.Abs(a) * 0.5f;
        }

        public static float AreaBelow(Vector2[][] pieces, Vector2 up, float c, List<Vector2> scratch) {
            float total = 0f;

            foreach (var piece in pieces) {
                ClipBelow(piece, up, c, scratch);
                if (scratch.Count >= 3) total += Area(scratch);
            }

            return total;
        }

        /*
        * Returns c such that the region dot(p, up) <= c holds targetArea of the interior.
        * up is the world "up" direction expressed in bottle-local space.
        */
        public static float SolveSurface(Vector2[][] pieces, Vector2 up, float targetArea, List<Vector2> scratch) {
            float lo = float.MaxValue, hi = float.MinValue;

            foreach (var piece in pieces)
                foreach (var p in piece) {
                    float d = Vector2.Dot(p, up);
                    lo = Mathf.Min(lo, d);
                    hi = Mathf.Max(hi, d);
                }

            for (int i = 0; i < 26; i++) {
                float mid = (lo + hi) * 0.5f;
                if (AreaBelow(pieces, up, mid, scratch) < targetArea) lo = mid; else hi = mid;
            }

            return (lo + hi) * 0.5f;
        }

        // World up in bottle-local space for a bottle rotated clockwise by tiltDeg.
        public static Vector2 LocalUp(float tiltDeg) {
            float r = tiltDeg * Mathf.Deg2Rad;

            return new Vector2(-Mathf.Sin(r), Mathf.Cos(r));
        }
    }

    // Where the bottle is held while pouring. Shared by the game and the headless tests.
    public static class PourPose {
        // The tilt where the bottle has finished lifting off the counter.
        public const float LiftDegrees = 40f;

        // Spot just above and left of the glass mouth where the outlet is held. The bottle is moved so
        // its spout ends up here, so liquid always leaves over the glass and falls into the opening.
        public static Vector2 OutletTarget(float glassCenterX, float mouthInnerRadius, float mouthY) =>
            new Vector2(glassCenterX - mouthInnerRadius * 0.35f, mouthY + 0.3f);

        /*
        * Bottle pivot (neck base) for a tilt. Upright it stands at rest; by LiftDegrees it has moved so the
        * outlet sits on the target, and from then on the pivot swings around the target.
        *
        * Distance from pivot to outlet in world units.
        */
        public static Vector2 Pivot(Vector2 rest, float tiltDeg, float outletDistance, Vector2 outletTarget) {
            float rad = tiltDeg * Mathf.Deg2Rad;
            Vector2 pourPivot = outletTarget - new Vector2(Mathf.Sin(rad), Mathf.Cos(rad)) * outletDistance;
            float blend = Mathf.SmoothStep(0f, 1f, tiltDeg / LiftDegrees);

            return Vector2.Lerp(rest, pourPivot, blend);
        }

        // Launch speed of the stream (units/s) for a given flow. A bigger flow shoots out a little faster,
        // which also makes the stream reach further out before gravity bends it down.
        public static float StreamSpeed(float flowMlPerSec) => 0.8f + 1.6f * Mathf.Sqrt(flowMlPerSec / 40f);
    }

    /*
    * Turns "how far the spout is below the liquid surface" into milliliters per second.
    * Flow follows Torricelli's law (speed grows with the square root of head) until the spout's
    * own restriction caps it. Viscous liquids need extra head to start and respond slowly, and
    * open necks surge as air glugs back into the bottle.
    *
    * In plain terms, each frame it answers "how fast is liquid coming out right now?":
    *   1. Head: tilt the bottle and the spout dips below the liquid line. The deeper it dips, the more
    *      pressure there is, so the flow rises. A bottle that is nearly empty has a lower liquid line,
    *      so it needs more tilt to pour.
    *   2. Target flow: the spout's maximum flow, scaled by the liquid's thickness and this order's random
    *      variance, scaled again by the square root of the head (fast at first, then levelling off).
    *   3. Glug: air has to get back into the bottle, so the flow pulses up and down a little.
    *   4. Lag: the real flow chases the target flow instead of jumping to it. Thick liquids chase slowly,
    *      which is why you have to ease off the tilt early to stop on the exact amount.
    */
    public class FlowModel {
        public float CurrentFlow { get; private set; }
        // Fires once per glug cycle so the audio can play a glug.
        public bool GluggedThisFrame { get; private set; }

        float glugPhase;

        public void Reset() {
            CurrentFlow = 0f;
            glugPhase = 0f;
            GluggedThisFrame = false;
        }

        /*
        * Depth of the outlet below the liquid surface, in bottle units.
        * How full the bottle is (0..1). Emptier bottles glug more.
        * Per-order random flow factor (spout variance).
        */
        public float Step(float head, float fillFraction, LiquidDef liquid, SpoutDef spout, float orderMultiplier, float dt) {
            GluggedThisFrame = false;
            float target = 0f;
            // Thick liquids need extra head before anything comes out (minHead), so subtract it first.
            // If h is zero or negative the spout is not deep enough in the liquid and nothing flows.
            float h = head - liquid.minHead;

            if (h > 0f) {
                // ramp goes from 0 to 1 as the head grows to the spout's rampHead. Taking its square root
                // gives the Torricelli shape: flow rises quickly at first, then flattens near the maximum.
                float ramp = Mathf.Clamp01(h / spout.rampHead);
                target = spout.maxFlowMlPerSec * liquid.flowMultiplier * orderMultiplier * Mathf.Sqrt(ramp);

                // Air has to get back into the bottle. Open necks and near-empty bottles do it in gulps.
                float glugAmount = spout.glug + (fillFraction < 0.3f ? 0.25f * (1f - fillFraction / 0.3f) : 0f);

                if (glugAmount > 0.01f) {
                    // The glug is a sine wave on the flow. rate is how many glugs per second (thick liquids
                    // glug half as often). Each time the phase passes a whole number, one glug sound plays.
                    float rate = Mathf.Lerp(3.5f, 6f, ramp) / (liquid.thick ? 2f : 1f);
                    float before = glugPhase;
                    glugPhase += rate * dt;
                    if (Mathf.Floor(before) != Mathf.Floor(glugPhase)) GluggedThisFrame = true;
                    target *= 1f + glugAmount * Mathf.Sin(glugPhase * Mathf.PI * 2f);
                }
            }

            /*
            * First-order lag toward the target. While the spout is below the surface, thick liquids
            * respond slowly in both directions, so easing off the tilt keeps them flowing for a moment:
            * the player has to back off early. Once the spout rises above the liquid the stream breaks
            * off quickly (a short drip, longer for thicker liquids) so nothing dribbles onto the counter.
            */
            float tau = h > 0f ? liquid.responseTime : StopTime(liquid);
            float k = 1f - Mathf.Exp(-dt / Mathf.Max(0.01f, tau));
            CurrentFlow += (target - CurrentFlow) * k;

            // Below a slow drip we call it stopped, so serving never waits on an invisible trickle.
            if (target <= 0f && CurrentFlow < 0.35f) CurrentFlow = 0f;

            return CurrentFlow;
        }

        // How fast the stream breaks off once the spout is above the liquid.
        public static float StopTime(LiquidDef liquid) => 0.05f + 0.12f * liquid.responseTime;
    }

    public enum Grade { Perfect, Great, Good, Short, Over, Spilled, Empty }

    // The result of grading one pour.</summary>
    public struct PourResult {
        public Grade grade;
        public float pouredMl, targetMl, errorMl, spilledMl;
        public float accuracy;   // 0..100
        public int points;
        public float tips;
    }

    // Grading rules. Tolerances scale with the order so half shots are not unfairly hard.
    public static class Scoring {
        public static float PerfectTolerance(float target) => Mathf.Max(1.5f, target * 0.05f);
        public static float GreatTolerance(float target) => Mathf.Max(3.0f, target * 0.10f);
        public static float GoodTolerance(float target) => Mathf.Max(5.0f, target * 0.18f);

        /*
        * Fraction of the order timer left (Classic only; pass 0 otherwise).
        * Consecutive Great-or-better pours before this one.
        * Keyboard-only controls score 25% more.
        */
        public static PourResult Evaluate(float poured, float target, float spilled, bool overflowed,
            float timeLeft01, int streak, bool hardMode) {
            var r = new PourResult { pouredMl = poured, targetMl = target, spilledMl = spilled };
            r.errorMl = poured - target;
            float abs = Mathf.Abs(r.errorMl);
            r.accuracy = Mathf.Clamp(100f - abs / target * 100f, 0f, 100f);

            if (poured < 0.5f) r.grade = Grade.Empty;
            else if (overflowed || spilled > Mathf.Max(4f, target * 0.25f)) r.grade = Grade.Spilled;
            else if (abs <= PerfectTolerance(target)) r.grade = Grade.Perfect;
            else if (abs <= GreatTolerance(target)) r.grade = Grade.Great;
            else if (abs <= GoodTolerance(target)) r.grade = Grade.Good;
            else r.grade = r.errorMl < 0 ? Grade.Short : Grade.Over;

            int basePts = 0;

            switch (r.grade) {
                case Grade.Perfect: 
                    basePts = 100; r.tips = 5f; 
                    break;
                case Grade.Great: 
                    basePts = 70; r.tips = 3f; 
                    break;
                case Grade.Good: 
                    basePts = 40; r.tips = 1.5f; 
                    break;
            }

            if (basePts > 0) {
                basePts += Mathf.RoundToInt(20f * Mathf.Clamp01(timeLeft01));
                basePts += Mathf.Min(streak, 5) * 10;
                r.tips += 0.5f * Mathf.Min(streak, 5);
            }

            // Small splashes over the rim cost a little even on a good pour.
            basePts -= Mathf.Min(30, Mathf.RoundToInt(spilled * 2f));
            if (hardMode) basePts = Mathf.RoundToInt(basePts * 1.25f);
            r.points = Mathf.Max(0, basePts);

            return r;
        }

        public static bool KeepsStreak(Grade g) => g == Grade.Perfect || g == Grade.Great;

        public static string Label(Grade g) {
            switch (g) {
                case Grade.Perfect: return "PERFECT!";
                case Grade.Great: return "GREAT!";
                case Grade.Good: return "GOOD";
                case Grade.Short: return "SHORT POUR";
                case Grade.Over: return "OVER POUR";
                case Grade.Spilled: return "SPILLED!";
                default: return "NOTHING POURED";
            }
        }

        public static Color ColorOf(Grade g) {
            switch (g) {
                case Grade.Perfect:
                    return new Color(1f, 0.82f, 0.2f);
                case Grade.Great:
                    return new Color(0.45f, 0.9f, 0.45f);
                case Grade.Good:
                    return new Color(0.5f, 0.8f, 1f);
                case Grade.Spilled:
                    return new Color(1f, 0.4f, 0.3f);
                default: 
                    return new Color(1f, 0.55f, 0.35f);
            }
        }

        // Max points per order before streak/hard-mode bonuses, used for star thresholds.
        public const int MaxBasePoints = 120;
    }
}