using System.Collections.Generic;
using UnityEngine;

namespace PourDecisions {
    /*
    * Loads every texture, font and the art manifest from Resources, and builds sprites at runtime.
    * Building sprites in code (instead of relying on import settings) means pivots and pixels-per-unit
    * always match the numbers the generator wrote into the manifest.
    */
    public static class Art {
        static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        static readonly Dictionary<string, Font> fonts = new Dictionary<string, Font>();
        static ArtManifest manifest;
        static Material spriteMaterial;

        public static ArtManifest Manifest {
            get {
                if (manifest == null) {
                    var json = Resources.Load<TextAsset>("Data/art_manifest");
                    manifest = JsonUtility.FromJson<ArtManifest>(json.text);
                }

                return manifest;
            }
        }

        // Converts a measurement in centimeters to world units.
        public static float CmToUnits(float cm) => cm / Manifest.cmPerUnit;

        public static GlassDef Glass(string id) => System.Array.Find(Manifest.glasses, g => g.id == id);
        public static BottleDef Bottle(string id) => System.Array.Find(Manifest.bottles, b => b.id == id);
        public static SpoutArt Spout(string id) =>
            id == null ? null : System.Array.Find(Manifest.spouts, s => s.id == id);

        /*
        * Unlit sprite material shared by every SpriteRenderer, mesh and line. Using one unlit material keeps
        * the game looking the same in the Built-in pipeline and in URP 2D (where the default is lit).
        */
        public static Material SpriteMaterial {
            get {
                if (spriteMaterial == null) {
                    // One simple unlit shader for everything, so lighting settings never change how the art looks.
                    Shader shader = Shader.Find("Sprites/Default");
                    if (shader == null) shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                    if (shader == null) shader = Shader.Find("UI/Default");
                    spriteMaterial = new Material(shader) { name = "PourDecisions Sprite" };
                }
                
                return spriteMaterial;
            }
        }

        /*
        * Gets (and caches) a sprite for Resources/Art/{name}.png.
        * Pixels per unit the art was drawn at.
        * Normalized pivot (0,0 = bottom left).
        * 9-slice border in pixels (left, bottom, right, top), or zero.
        */
        public static Sprite Sprite(string name, float ppu, Vector2 pivot, Vector4 border = default) {
            // Sprites are cached by name and settings so each one is created only once.
            string key = name + "|" + ppu + "|" + pivot.x + "," + pivot.y + "|" + border.x;
            if (sprites.TryGetValue(key, out var cached)) return cached;
            // Resources.Load finds files inside any folder called Resources, with no file extension.
            var tex = Resources.Load<Texture2D>("Art/" + name);

            if (tex == null) {
                Debug.LogError("[Pour Decisions] Missing art: Resources/Art/" + name);
                tex = Texture2D.whiteTexture;
            }

            tex.wrapMode = TextureWrapMode.Clamp;
            var s = UnityEngine.Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), pivot, ppu, 0, SpriteMeshType.FullRect, border);
            s.name = name;
            sprites[key] = s;

            return s;
        }

        // Sprite with a centered pivot drawn at 100 pixels per unit (backgrounds, UI, fx).
        public static Sprite Sprite(string name) => Sprite(name, 100f, new Vector2(0.5f, 0.5f));

        // Sprite for UI images that stretch with 9-slicing.
        public static Sprite Sliced(string name, float border) =>
            Sprite(name, 100f, new Vector2(0.5f, 0.5f), new Vector4(border, border, border, border));

        public static Font Font(string name) {
            if (fonts.TryGetValue(name, out var f)) return f;
            f = Resources.Load<Font>("Fonts/" + name);
            fonts[name] = f;

            return f;
        }

        public static Font Display => Font("LilitaOne-Regular");
        public static Font Body => Font("Nunito-Bold");
        public static Font BodyHeavy => Font("Nunito-Black");

        // Creates a GameObject with a SpriteRenderer using the shared material.
        public static SpriteRenderer NewSprite(string objName, Transform parent, Sprite sprite, int order) {
            var go = new GameObject(objName);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sharedMaterial = SpriteMaterial;
            sr.sortingOrder = order;

            return sr;
        }

        // Creates a GameObject with a MeshFilter/MeshRenderer for procedural shapes (liquids).
        public static MeshFilter NewMesh(string objName, Transform parent, int order) {
            var go = new GameObject(objName);
            go.transform.SetParent(parent, false);
            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = SpriteMaterial;
            mr.sortingOrder = order;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            var mesh = new Mesh { name = objName };
            mesh.MarkDynamic();
            mf.sharedMesh = mesh;

            return mf;
        }
    }

    // Sorting orders for every world layer, back to front.
    public static class Layers {
        public const int Background = -100;
        public const int Customer = -60;
        public const int Counter = -40;
        public const int Puddle = -39;
        public const int GlassBack = 0;
        public const int GlassLiquid = 1;
        public const int GlassSurface = 2;
        public const int TargetLine = 3;
        public const int Stream = 4;
        public const int GlassFront = 5;
        public const int Fx = 6;
        public const int BottleBack = 10;
        public const int BottleLiquid = 11;
        public const int BottleFront = 12;
        public const int Spout = 13;
        public const int Darkness = 50;
        public const int Popups = 60;
    }
}