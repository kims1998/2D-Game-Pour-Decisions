using System.Collections.Generic;
using UnityEngine;

namespace PourDecisions {
    /*
    * The glass being filled. Holds the real volume (ml) and draws the liquid as a mesh whose shape comes
    * straight from the frustum math, so what you see is exactly what is scored.
    * The surface ripples when liquid lands and settles when you stop.
    */
    public class Glass : MonoBehaviour {
        public GlassDef Def { get; private set; }
        public float CurrentMl { get; private set; }
        public float OverflowMl { get; private set; }
        public bool Overflowed => OverflowMl > 0.5f;

        // Interior dimensions in world units.
        float rb, rt, h, baseY, wall;
        LiquidDef liquid;
        Color ambient = Color.white;

        SpriteRenderer back, front;
        MeshFilter liquidMesh, surfaceMesh;
        readonly List<SpriteRenderer> dashes = new List<SpriteRenderer>();
        float wobble;
        float rippleX;

        const int Columns = 18;
        readonly List<Vector3> verts = new List<Vector3>();
        readonly List<Color> colors = new List<Color>();
        readonly List<int> tris = new List<int>();
        readonly List<Vector2> surface = new List<Vector2>();

        public void Init(GlassDef def) {
            Def = def;
            // Glass sizes are stored in centimeters. Convert them to Unity world units once, here.
            // rb/rt are the inside radius at the bottom/top, h the inside height, wall the glass thickness.
            rb = Art.CmToUnits(def.bottomRadiusCm);
            rt = Art.CmToUnits(def.topRadiusCm);
            h = Art.CmToUnits(def.heightCm);
            baseY = Art.CmToUnits(def.baseCm);
            wall = Art.CmToUnits(def.wallCm);
            float ppu = Art.Manifest.pixelsPerUnit;
            var pivot = new Vector2(def.pivotX, def.pivotY);
            // "Back" is the rear wall of the glass and "Front" the front wall. The liquid mesh is drawn
            // between them (see Layers in Art.cs), which makes the liquid look like it is inside.
            back = Art.NewSprite("Back", transform, Art.Sprite($"glass_{def.id}_back", ppu, pivot), Layers.GlassBack);
            front = Art.NewSprite("Front", transform, Art.Sprite($"glass_{def.id}_front", ppu, pivot), Layers.GlassFront);
            liquidMesh = Art.NewMesh("Liquid", transform, Layers.GlassLiquid);
            surfaceMesh = Art.NewMesh("Surface", transform, Layers.GlassSurface);
            var white = Art.Sprite("fx_white");

            // Seven small sprites make up the dotted target line. They stay hidden until ShowTarget.
            for (int i = 0; i < 7; i++) {
                var d = Art.NewSprite("TargetDash", transform, white, Layers.TargetLine);
                d.gameObject.SetActive(false);
                dashes.Add(d);
            }
        }

        public void SetLiquid(LiquidDef l) => liquid = l;

        // Darkens everything for dim-light levels.
        public void SetAmbient(Color c) {
            ambient = c;
            back.color = c;
            front.color = c;
        }

        // ------------------------------------------------------------ geometry queries (world space)

        public float CenterX => transform.position.x;
        public float BottomY => transform.position.y + baseY;
        public float MouthY => transform.position.y + baseY + h;
        public float MouthInnerRadius => rt;
        public float Wall => wall;
        public float HeightUnits => baseY + h;

        // Current liquid height in centimeters. CurrentMl is a volume, and because the glass is a cone the
        // height is not simply proportional to it, so the cone volume formula is inverted (CupMath).
        public float LevelCm => CupMath.HeightForVolume(Def.bottomRadiusCm, Def.topRadiusCm, Def.heightCm, CurrentMl);

        // World height of the liquid surface right now. A falling parcel is added to the glass when it
        // reaches this height.
        public float SurfaceY => BottomY + Art.CmToUnits(LevelCm);
        public float Fill01 => Mathf.Clamp01(LevelCm / Def.heightCm);

        // The inside radius of the glass at a world height. Glasses are cones, so the radius changes
        // smoothly from the bottom (rb) to the rim (rt). The stream uses this to keep liquid between the walls.
        public float InnerRadiusAtWorldY(float y) =>
            Mathf.Lerp(rb, rt, Mathf.Clamp01((y - BottomY) / h));

        // World Y of the surface if the glass held this much liquid.
        public float WorldYForMl(float ml) =>
            BottomY + Art.CmToUnits(CupMath.HeightForVolume(Def.bottomRadiusCm, Def.topRadiusCm, Def.heightCm, ml));

        // ------------------------------------------------------------ filling

        /*
        * Receives liquid from the pour. PourStream calls this once for every parcel that lands on the
        * surface, with the milliliters it carries and the world x where it landed.
        * The glass keeps one number, CurrentMl, and everything else follows from it: the liquid height,
        * the mesh that is drawn, and the score. Nothing is ever counted before it lands.
        */
        public void AddLiquid(float ml, float x) {
            // The glass only accepts what fits. Anything extra runs over the rim and counts as overflow.
            // space = how many ml are left before the glass is full.
            float space = Def.capacityMl - CurrentMl;
            float accepted = Mathf.Min(ml, Mathf.Max(0f, space));
            CurrentMl += accepted;
            float over = ml - accepted;

            // Overflow is tracked separately. If more than half a milliliter runs over the rim, the glass
            // counts as overflowed and the pour is graded as spilled.
            if (over > 0f) {
                OverflowMl += over;

                if (Random.value < 0.6f && liquid != null) {
                    float side = Random.value < 0.5f ? -1f : 1f;
                    Fx.I.Drip(new Vector2(CenterX + side * (rt + wall * 0.6f), MouthY), liquid.color, side);
                }
            }
            
            // Ripples scale with the impact; a thin stream barely disturbs the surface.
            // Each landing adds a ripple. It fades out over time in Update.
            wobble = Mathf.Min(1f, wobble + ml * 0.06f);
            rippleX = x - CenterX;
        }

        // Shows a dashed line at the height the target amount would reach.
        public void ShowTarget(float ml, bool show, Color color) {
            // Find the height the target volume would reach, then spread the dashes across the glass width there.
            float y = WorldYForMl(ml) - transform.position.y;
            float r = Mathf.Lerp(rb, rt, Mathf.Clamp01((y - baseY) / h));

            for (int i = 0; i < dashes.Count; i++) {
                var d = dashes[i];
                d.gameObject.SetActive(show);
                if (!show) continue;
                float t = (i + 0.5f) / dashes.Count;
                float x = Mathf.Lerp(-r * 0.92f, r * 0.92f, t);
                d.transform.localPosition = new Vector3(x, y, 0f);
                float len = r * 1.84f / dashes.Count * 0.6f;
                d.transform.localScale = new Vector3(len / 0.04f, 0.022f / 0.04f, 1f);
                d.color = color;
            }
        }

        // ------------------------------------------------------------ drawing

        void Update() {
            wobble = Mathf.Max(0f, wobble - Time.deltaTime * 0.9f);
        }

        void LateUpdate() => Redraw();

        // Rebuilds the liquid meshes from the current volume.
        public void Redraw() {
            if (Def == null) return;
            BuildLiquid();
        }

        float SurfaceOffset(float x, float t) {
            float amp = 0.035f * wobble;
            float k = 1f / Mathf.Max(0.2f, rt);
            float dist = x - rippleX;

            return amp * (Mathf.Sin(dist * 9f * k - t * 11f) * 0.7f + Mathf.Sin(x * 17f * k + t * 7f) * 0.3f);
        }

        void BuildLiquid() {
            var mesh = liquidMesh.sharedMesh;
            var smesh = surfaceMesh.sharedMesh;
            mesh.Clear();
            smesh.Clear();
            if (liquid == null || CurrentMl <= 0.05f) return;

            // Turn the stored volume into a height, then rebuild the liquid shape for this frame.
            float level = Art.CmToUnits(LevelCm);
            float rTop = Mathf.Lerp(rb, rt, level / h);
            float top = baseY + level;
            float t = Time.time;
            Color body = liquid.color * ambient;
            Color deep = new Color(body.r * 0.72f, body.g * 0.72f, body.b * 0.72f, Mathf.Min(1f, body.a + 0.08f));
            Color lip = Color.Lerp(body, Color.white * ambient, 0.45f);
            lip.a = Mathf.Max(0.85f, body.a);

            // Surface line across the glass, rippling and kept inside the walls.
            // The surface is a row of points across the glass, each nudged up or down by a small ripple.
            surface.Clear();

            for (int i = 0; i <= Columns; i++) {
                float x = Mathf.Lerp(-rTop, rTop, i / (float)Columns);
                surface.Add(new Vector2(x, Mathf.Clamp(top + SurfaceOffset(x, t), baseY, baseY + h)));
            }

            // Body: from the glass bottom up to the surface, darker at the bottom.
            // Body of the liquid: a strip of quads from the glass bottom up to the surface, darker at the bottom.
            BuildStrip(mesh, i => new Vector3(Mathf.Lerp(-rb, rb, i / (float)Columns), baseY, 0f), deep,
                i => surface[i], body);

            // A bright meniscus band makes even clear liquids (vodka, water) readable.
            float band = Mathf.Min(0.035f, level);
            BuildStrip(smesh, i => surface[i] - new Vector2(0f, band), new Color(lip.r, lip.g, lip.b, 0f),
                i => surface[i] + new Vector2(0f, 0.006f), lip);
        }

        /*
        * Fills a mesh with a strip of quads between a lower and an upper edge, one column per surface
        * point, with a vertical color gradient.
        */
        void BuildStrip(Mesh mesh, System.Func<int, Vector3> lower, Color lowerColor,
            System.Func<int, Vector3> upper, Color upperColor) {
            verts.Clear(); colors.Clear(); tris.Clear();

            for (int i = 0; i <= Columns; i++) {
                verts.Add(lower(i)); colors.Add(lowerColor);
                verts.Add(upper(i)); colors.Add(upperColor);
                if (i == 0) continue;
                int a = (i - 1) * 2;
                tris.Add(a); tris.Add(a + 1); tris.Add(a + 3);
                tris.Add(a); tris.Add(a + 3); tris.Add(a + 2);
            }

            mesh.SetVertices(verts);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
        }
    }
}