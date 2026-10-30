using System.Collections.Generic;
using UnityEngine;

namespace PourDecisions {
    /*
    * A bottle the player tilts. Each frame it:
    *  1. poses itself (lifts off the counter, then swings so the spout stays over the glass),
    *  2. finds the liquid surface inside the tilted bottle (BottleMath),
    *  3. measures how far the spout is below that surface (the "head") and runs the FlowModel,
    *  4. sends the poured milliliters into the PourStream as liquid in flight.
    * A fuller bottle therefore starts pouring at a shallower angle, and the flow tapers as it drains.
    */
    public class Bottle : MonoBehaviour {
        // A "full" bottle still has an air gap in the neck, so liquid never fills the whole interior.
        const float FullAreaFraction = 0.88f;

        public BottleDef Def { get; private set; }
        public LiquidDef Liquid { get; private set; }
        public SpoutDef Spout { get; private set; }
        public float Ml { get; private set; }
        public float Tilt { get; private set; }
        public float Flow => flow.CurrentFlow;
        public float Fill01 => Def == null ? 0f : Ml / Def.capacityMl;

        // Where the bottle stands when upright (world position of its neck base).
        public Vector2 RestPivot { get; set; }

        Vector2[][] pieces;
        float totalArea;
        float outletY;
        float orderMultiplier = 1f;
        readonly FlowModel flow = new FlowModel();
        readonly List<Vector2> scratch = new List<Vector2>();
        readonly List<Vector3> verts = new List<Vector3>();
        readonly List<Color> colors = new List<Color>();
        readonly List<int> tris = new List<int>();

        SpriteRenderer back, front, spoutRenderer;
        MeshFilter liquidMesh;
        Color ambient = Color.white;
        float wiggle;

        // Starting fill as a fraction of the bottle's capacity.
        // This order's random spout variance.
        public void Init(BottleDef def, LiquidDef liquid, SpoutDef spout, float fill01, float flowMultiplier) {
            // Remember which bottle, liquid and spout this is, and start with the requested fill.
            Def = def;
            Liquid = liquid;
            Spout = spout;
            Ml = def.capacityMl * Mathf.Clamp01(fill01);
            orderMultiplier = flowMultiplier;
            flow.Reset();

            // The bottle interior comes from the art manifest as a few convex polygons.
            // Copy them into Vector2 arrays and add up the total interior area.
            pieces = new Vector2[def.interior.Length][];
            totalArea = 0f;

            for (int i = 0; i < pieces.Length; i++) {
                var pts = def.interior[i].points;
                pieces[i] = new Vector2[pts.Length];
                for (int j = 0; j < pts.Length; j++) pieces[i][j] = new Vector2(pts[j].x, pts[j].y);
                totalArea += BottleMath.Area(pieces[i]);
            }

            // Three layers, back to front: the bottle back, the liquid mesh, then the bottle front.
            // The liquid sits between them so it looks like it is inside the glass.
            float ppu = Art.Manifest.pixelsPerUnit;
            var pivot = new Vector2(def.pivotX, def.pivotY);
            back = Art.NewSprite("Back", transform, Art.Sprite($"bottle_{def.id}_back", ppu, pivot), Layers.BottleBack);
            liquidMesh = Art.NewMesh("Liquid", transform, Layers.BottleLiquid);
            front = Art.NewSprite("Front", transform, Art.Sprite($"bottle_{def.id}_front", ppu, pivot), Layers.BottleFront);

            // The spout is a separate sprite on top of the neck. outletY is where liquid leaves.
            var spoutArt = Art.Spout(spout.art);
            outletY = def.neckTopY;

            if (spoutArt != null) {
                var s = Art.Sprite("spout_" + spoutArt.id, ppu, new Vector2(0.5f, spoutArt.pivotY));
                spoutRenderer = Art.NewSprite("Spout", transform, s, Layers.Spout);
                spoutRenderer.transform.localPosition = new Vector3(0f, def.neckTopY - 0.06f, 0f);
                outletY = def.neckTopY - 0.06f + spoutArt.length;
            }

            transform.localScale = Vector3.one * K.BottleScale;
        }

        public void SetAmbient(Color c) {
            ambient = c;
            back.color = c;
            front.color = c;
            if (spoutRenderer != null) spoutRenderer.color = c;
        }

        // World position of the pour outlet.
        public Vector2 OutletWorld => transform.TransformPoint(new Vector3(0f, outletY, 0f));

        /*
        * Advance one frame.
        * Requested tilt, 0 = upright, clockwise positive.
        * The glass to pour over, or null to stay at rest.
        */
        public void Tick(float tiltDeg, float dt, Glass glass, PourStream stream) {
            Tilt = tiltDeg;
            /*
            * One frame of pouring, in order:
            *   1. Pose: rotate the bottle to the tilt and move it so its spout is over the glass.
            *   2. Surface: work out where the liquid line sits inside the tilted bottle.
            *   3. Head: measure how far the spout is below that line (deeper = faster flow).
            *   4. Flow: FlowModel turns the head into milliliters per second for this frame.
            *   5. Emit: hand that many milliliters to the PourStream, which flies them into the glass.
            *   6. Draw: rebuild the liquid mesh drawn inside the bottle.
            */
            Pose(glass);

            // "Up" in the bottle's own coordinates. When the bottle is upright this is straight up;
            // as it tilts, "up" swings sideways, which is what lets the liquid line slope toward the spout.
            Vector2 up = BottleMath.LocalUp(Tilt);
            // Liquid area = how full the bottle is, times its interior area (leaving an air gap at the top).
            float area = Fill01 * FullAreaFraction * totalArea;
            // The bottle interior is a few polygons. SolveSurface finds the liquid line that leaves exactly
            // this much area of the interior underneath it, so a fuller bottle has a higher line.
            float surface = BottleMath.SolveSurface(pieces, up, area, scratch);
            // Head = height of the liquid line above the outlet, measured along "up". A positive head
            // means the spout is under the liquid, so liquid can come out. Negative means it is dry.
            float head = surface - Vector2.Dot(new Vector2(0f, outletY), up);

            float q = flow.Step(head, Fill01, Liquid, Spout, orderMultiplier, dt);
            // Flow (ml per second) times the frame time gives the ml leaving this frame.
            // Never pour more than is left in the bottle.
            float ml = Mathf.Min(q * dt, Ml);

            if (ml > 0f) {
                // Take the liquid out of the bottle now. It is in the air from here until it lands in the
                // glass, and the glass only counts it once it lands (see PourStream.Step).
                Ml -= ml;
                // The stream leaves along the direction the bottle points, plus a little random jitter for glugs.
                Vector2 axis = transform.up;
                Vector2 vel = axis * PourPose.StreamSpeed(q) + Random.insideUnitCircle * (Spout.glug * 0.25f);
                stream.Emit(OutletWorld, vel, ml, Liquid, q);
            } else {
                // Nothing came out, so the current stream segment ends.
                stream.CloseRun();
            }

            if (flow.GluggedThisFrame && ml > 0f) {
                AudioManager.I.Glug(Fill01);
                wiggle = 1f;
            }

            BuildLiquidMesh(up, surface);
        }

        /*
        * Upright the bottle stands on the counter. As it tilts it lifts toward the glass, and past
        * PourPose.LiftDegrees the pivot moves so the outlet stays just above and left of the glass mouth
        * (like a bartender's wrist keeping the spout over the glass).
        */
        void Pose(Glass glass) {
            wiggle = Mathf.Max(0f, wiggle - Time.deltaTime * 6f);
            float shake = Mathf.Sin(Time.time * 70f) * wiggle * 1.2f;
            // Unity rotates counter-clockwise but we tilt clockwise, so the angle is negated.
            transform.rotation = Quaternion.Euler(0f, 0f, -(Tilt + shake));
            // With no glass the bottle just stands at its rest spot. With a glass, PourPose moves it
            // over the glass as it tilts.
            Vector2 pos = RestPivot;

            if (glass != null) {
                Vector2 target = PourPose.OutletTarget(glass.CenterX, glass.MouthInnerRadius, glass.MouthY);
                pos = PourPose.Pivot(RestPivot, Tilt, outletY * K.BottleScale, target);
            }
            
            transform.position = new Vector3(pos.x, pos.y, 0f);
        }

        // Fills the bottle interior below the surface line with a mesh (in bottle-local space).
        void BuildLiquidMesh(Vector2 up, float surface) {
            // Reuse the same mesh every frame instead of creating a new one (cheaper for the garbage collector).
            var mesh = liquidMesh.sharedMesh;
            mesh.Clear();
            if (Ml <= 0.5f) return;
            verts.Clear(); colors.Clear(); tris.Clear();
            // Build the liquid shape: clip each interior polygon at the water line and fill what is below it.
            Color c = Liquid.color * ambient;
            c.a = Mathf.Max(0.55f, Liquid.color.a);
            Color lighter = Color.Lerp(c, Color.white * ambient, 0.25f);

            foreach (var piece in pieces) {
                BottleMath.ClipBelow(piece, up, surface, scratch);
                if (scratch.Count < 3) continue;
                int start = verts.Count;

                foreach (var p in scratch) {
                    verts.Add(new Vector3(p.x, p.y, 0f));
                    // slightly lighter near the surface
                    float depth = surface - Vector2.Dot(p, up);
                    colors.Add(depth < 0.05f ? lighter : c);
                }

                for (int i = 1; i < scratch.Count - 1; i++) {
                    tris.Add(start); tris.Add(start + i); tris.Add(start + i + 1);
                }
            }

            mesh.SetVertices(verts);
            mesh.SetColors(colors);
            // The sprite shader renders both faces (Cull Off), so polygon winding does not matter.
            mesh.SetTriangles(tris, 0);
        }
    }
}