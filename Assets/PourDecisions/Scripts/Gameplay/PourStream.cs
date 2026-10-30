using System.Collections.Generic;
using UnityEngine;

namespace PourDecisions {
    /*
    * Liquid in flight. The bottle emits small parcels of liquid (each carrying its milliliters); they fly
    * under gravity and are only counted when they actually land in the glass. Parcels that hit the rim
    * or miss the glass become spills. Because of this, liquid already in the air still lands after you
    * stop tilting, exactly like a real pour.
    * Consecutive parcels are drawn as one tapered LineRenderer per continuous "run" of pouring.
    */
    public class PourStream : MonoBehaviour {
        struct Parcel {
            public Vector2 pos, vel;
            public float ml, age;
            public bool inside;
        }

        class Run {
            public readonly List<Parcel> parcels = new List<Parcel>();
            public LineRenderer line;
            public bool open;
            public Vector2 outlet;
            public float width;
            public Color color;
        }

        public Glass Target { get; set; }

        // Milliliters currently in the air.
        public float InFlightMl { get; private set; }
        public bool Busy => runs.Count > 0;
        // Smoothed ml/s landing in the glass (drives the pour sound).
        public float LandingRate { get; private set; }
        // Liquid that missed the glass or bounced off the rim this order.
        public float SpilledMl { get; private set; }

        readonly List<Run> runs = new List<Run>();
        readonly Stack<LineRenderer> linePool = new Stack<LineRenderer>();
        readonly List<Vector3> points = new List<Vector3>();
        Run current;
        LiquidDef liquid;
        Color ambient = Color.white;
        float landedThisFrame;
        float splashCooldown;

        public void SetAmbient(Color c) => ambient = c;

        public void ResetOrder() {
            ClearAll();
            SpilledMl = 0f;
            LandingRate = 0f;
        }

        /*
        * Adds liquid leaving the outlet this frame.
        * Called by the bottle every frame it pours. Each call adds one "parcel": a tiny blob of liquid
        * with a position, a velocity and the milliliters it carries.
        */
        public void Emit(Vector2 outlet, Vector2 velocity, float ml, LiquidDef l, float flowMlPerSec) {
            liquid = l;

            // Parcels from consecutive frames join the same run (one continuous stream).
            // A new run starts after the pour has stopped and restarted.
            if (current == null || !current.open) {
                current = new Run { open = true, line = GetLine() };
                runs.Add(current);
            }

            current.outlet = outlet;
            current.width = Mathf.Clamp(0.028f + 0.05f * Mathf.Sqrt(flowMlPerSec / 25f), 0.025f, 0.11f) * (l.thick ? 1.25f : 1f);
            current.color = l.color;
            current.parcels.Add(new Parcel { pos = outlet, vel = velocity, ml = ml });
        }

        // Called when nothing came out this frame: the next drop starts a new stream segment.
        public void CloseRun() {
            if (current != null) current.open = false;
        }

        public void ClearAll() {
            foreach (var r in runs) ReleaseLine(r.line);
            runs.Clear();
            current = null;
            InFlightMl = 0f;
        }

        public void Tick(float dt) {
            landedThisFrame = 0f;
            splashCooldown -= dt;
            InFlightMl = 0f;

            // Move every parcel, newest run last. Loops go backwards so we can delete items while looping.
            for (int ri = runs.Count - 1; ri >= 0; ri--) {
                var run = runs[ri];
                for (int i = run.parcels.Count - 1; i >= 0; i--) {
                    var p = run.parcels[i];
                    if (Step(ref p, dt)) run.parcels.RemoveAt(i);
                    else { run.parcels[i] = p; InFlightMl += p.ml; }
                }

                if (run.parcels.Count == 0 && !run.open) {
                    ReleaseLine(run.line);
                    runs.RemoveAt(ri);
                    if (run == current) current = null;
                } else Draw(run);
            }

            // ml per second landing in the glass this frame, smoothed so the pour sound does not flicker.
            float rate = dt > 0f ? landedThisFrame / dt : 0f;
            LandingRate += (rate - LandingRate) * (1f - Mathf.Exp(-dt / 0.08f));
        }

        /*
        * Moves one parcel by one frame and decides what happens to it. Returns true when the parcel is
        * finished, which means it landed in the glass or it spilled. A parcel goes through these stages:
        *   1. Falling: gravity pulls it down while it keeps its sideways speed from the spout.
        *   2. Reaching the rim: the frame it passes the height of the glass mouth, its x position decides:
        *      inside the opening (it goes in), on the glass wall (it splashes and spills), or outside
        *      (it keeps falling).
        *   3. Inside the glass: it stays between the walls until it touches the liquid surface, then its
        *      milliliters are added to the glass. This is the only place poured liquid is counted.
        *   4. Missing: if it never entered the glass and reaches the counter, it becomes a puddle.
        */
        bool Step(ref Parcel p, float dt) {
            Vector2 from = p.pos;
            // Gravity pulls the parcel down; then move it by velocity times time.
            p.vel.y -= K.Gravity * dt;
            Vector2 to = from + p.vel * dt;
            p.age += dt;
            var g = Target;

            // Did this parcel just cross the height of the glass rim? If so, decide where it crossed:
            // inside the opening (it goes in), on the rim (it spills), or outside (it keeps falling).
            if (g != null && !p.inside && from.y >= g.MouthY && to.y < g.MouthY) {
                // Crossing the rim plane: inside the mouth, on the rim, or outside the glass?
                float t = (from.y - g.MouthY) / (from.y - to.y);
                float x = Mathf.Lerp(from.x, to.x, t);
                float dx = Mathf.Abs(x - g.CenterX);
                if (dx < g.MouthInnerRadius) p.inside = true;
                else if (dx < g.MouthInnerRadius + g.Wall + 0.02f) {
                    Spill(p, new Vector2(x, g.MouthY), true);
                    return true;
                }
            }

            // Once inside the glass: stay between the walls, and when it reaches the liquid surface,
            // add its milliliters to the glass. This is the only place poured liquid is counted.
            if (g != null && p.inside) {
                // Bounce off the inner wall (matters for wide streams in tapered glasses).
                float r = g.InnerRadiusAtWorldY(to.y) - 0.015f;

                if (Mathf.Abs(to.x - g.CenterX) > r) {
                    to.x = g.CenterX + Mathf.Sign(to.x - g.CenterX) * r;
                    p.vel.x *= -0.25f;
                }

                if (to.y <= g.SurfaceY) {
                    g.AddLiquid(p.ml, to.x);
                    landedThisFrame += p.ml;

                    if (splashCooldown <= 0f && p.ml > 0.15f) {
                        splashCooldown = 0.07f;
                        Fx.I.Droplets(new Vector2(to.x, g.SurfaceY), liquid.color, 1, 0.9f, false);
                    }

                    return true;
                }
            }

            // Fell past the rim without going in and hit the counter: it is a spill.
            if (!p.inside && to.y <= K.CounterY) {
                Spill(p, new Vector2(to.x, K.CounterY), false);
                return true;
            }

            // Safety net: a parcel that has been alive for 4 seconds is counted as spilled and removed.
            if (p.age > 4f) {
                SpilledMl += p.ml;
                return true;
            }

            p.pos = to;
            return false;
        }

        // Records a parcel that did not make it into the glass. Spilled liquid is subtracted from the score,
        // and a spill on the rim gets a splash while a spill on the counter also leaves a puddle.
        void Spill(Parcel p, Vector2 at, bool rim) {
            SpilledMl += p.ml;
            if (liquid == null) return;
            Fx.I.Droplets(at, liquid.color, rim ? 3 : 2, rim ? 1.4f : 1.0f, true);
            if (!rim) Fx.I.Puddle(at.x, liquid.color, p.ml);
            AudioManager.I.Splash(rim ? 0.5f : 0.4f);
        }

        void Draw(Run run) {
            // Draw the stream as a line through the outlet and every parcel, newest to oldest.
            points.Clear();
            if (run.open) points.Add(run.outlet);
            for (int i = run.parcels.Count - 1; i >= 0; i--) points.Add(run.parcels[i].pos);
            var lr = run.line;

            if (points.Count < 2) {
                lr.positionCount = 0;
                return;
            }

            lr.positionCount = points.Count;
            lr.SetPositions(points.ToArray());
            lr.widthMultiplier = run.width;
            Color c = run.color * ambient;
            c.a = Mathf.Max(0.7f, run.color.a);
            lr.startColor = c;
            lr.endColor = c;
        }

        LineRenderer GetLine() {
            LineRenderer lr;

            // Reuse old LineRenderers from a pool instead of creating and destroying them constantly.
            if (linePool.Count > 0) {
                lr = linePool.Pop();
                lr.gameObject.SetActive(true);
            } else {
                var go = new GameObject("StreamRun");
                go.transform.SetParent(transform, false);
                lr = go.AddComponent<LineRenderer>();
                lr.sharedMaterial = Art.SpriteMaterial;
                lr.sortingOrder = Layers.Stream;
                lr.useWorldSpace = true;
                lr.numCapVertices = 4;
                lr.numCornerVertices = 2;
                lr.alignment = LineAlignment.View;
                lr.textureMode = LineTextureMode.Stretch;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;
                // The stream thins slightly as it falls and speeds up.
                lr.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.7f));
            }

            lr.positionCount = 0;
            return lr;
        }

        void ReleaseLine(LineRenderer lr) {
            if (lr == null) return;
            lr.positionCount = 0;
            lr.gameObject.SetActive(false);
            linePool.Push(lr);
        }
    }
}