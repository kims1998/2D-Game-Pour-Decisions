using System;
using UnityEngine;

namespace PourDecisions {
    // The two ways to play.
    public enum GameMode { Classic, Training }

    // World layout and unit constants shared by every system.
    public static class K {
        // One US shot (1.5 fl oz) in milliliters.
        public const float ShotMl = 44.36f;

        // World Y where glasses and bottles stand on the counter.
        public const float CounterY = -2.8f;
        // World Y of the counter's back edge (customers are hidden below it).
        public const float CounterBackY = -2.55f;
        // World X where the glass being filled stops.
        public const float GlassX = 1.2f;
        // Default orthographic camera size (half of the 10.8 unit tall view).
        public const float BaseOrtho = 5.4f;
        // Gravity for liquid in flight, in units per second squared.
        public const float Gravity = 150f;
        // Bottles are drawn a little larger than their art for readability.
        public const float BottleScale = 1.15f;
    }

    // ------------------------------------------------------------------ art manifest (written by Tools/generate_art.py)

    [Serializable] public class Pt { public float x, y; }
    [Serializable] public class Poly { public Pt[] points; }

    // A glass: real interior size in centimeters plus sprite placement info.
    [Serializable]
    public class GlassDef {
        public string id, name;
        public float bottomRadiusCm, topRadiusCm, heightCm, baseCm, wallCm, capacityMl;
        public float pivotX, pivotY;
        public int widthPx, heightPx;
    }

    // A bottle: interior as convex polygons (local units, origin at the neck base).
    [Serializable]
    public class BottleDef {
        public string id, brand;
        public float capacityMl;
        public Poly[] interior;
        public float neckTopY, bodyBottomY, bodyHalfWidth;
        public float pivotX, pivotY;
        public int widthPx, heightPx;
    }

    [Serializable]
    public class SpoutArt {
        public string id;
        public float length, pivotY;
        public int widthPx, heightPx;
    }

    [Serializable]
    public class ArtManifest {
        public float cmPerUnit, pixelsPerUnit;
        public GlassDef[] glasses;
        public BottleDef[] bottles;
        public SpoutArt[] spouts;
    }

    // ------------------------------------------------------------------ gameplay definitions

    // How a liquid looks and flows.
    public class LiquidDef {
        public string id, name, bottleId;
        public Color color;
        // Multiplies the spout's max flow (thick liquids are well under 1).
        public float flowMultiplier;
        // Extra head (units) needed before the liquid starts moving. Acts like "needs more tilt".
        public float minHead;
        // Seconds for the stream to speed up or trail off. High values keep dribbling after you stop.
        public float responseTime;
        // Uses the low, slow pour sound.
        public bool thick;

        public LiquidDef(string id, string name, string bottleId, Color color, float flowMultiplier,
            float minHead, float responseTime, bool thick) {
            this.id = id; this.name = name; this.bottleId = bottleId; this.color = color;
            this.flowMultiplier = flowMultiplier; this.minHead = minHead; this.responseTime = responseTime;
            this.thick = thick;
        }
    }

    // A spout (or no spout). Each one changes how fast and how evenly the bottle pours.
    public class SpoutDef {
        public string id, name;
        // Art id from the manifest, or null for an open neck.
        public string art;
        public float maxFlowMlPerSec;
        // Head (units) over which flow ramps from zero to max. Bigger = more analog control.
        public float rampHead;
        // 0..1 strength of the glug-glug surge as air gulps back into the bottle.
        public float glug;
        // Random per-order change in flow (0.1 = plus or minus 10%), so no two pours are identical.
        public float variance;

        public SpoutDef(string id, string name, string art, float maxFlow, float rampHead, float glug, float variance) {
            this.id = id; this.name = name; this.art = art; maxFlowMlPerSec = maxFlow; this.rampHead = rampHead;
            this.glug = glug; this.variance = variance;
        }
    }

    // One Classic-mode level: which glasses, spouts and drinks can show up, and how strict it is.
    public class LevelDef {
        public string name, blurb;
        public int orders;
        public float secondsPerOrder;
        public string[] glasses, spouts, liquids;
        public float[] targetShots;
        // 0 = bright, 1 = very dim. Dim levels hide dark liquids so players lean on the pour sound.
        public float dim;
    }

    // All static game content in one place so balancing happens in a single file.
    public static class GameData {
        static Color C(float r, float g, float b, float a) => new Color(r, g, b, a);

        public static readonly LiquidDef[] Liquids = {
            new LiquidDef("whiskey", "Whiskey", "whiskey", C(0.80f, 0.45f, 0.12f, 0.88f), 1.00f, 0.00f, 0.07f, false),
            new LiquidDef("vodka", "Vodka", "vodka", C(0.80f, 0.90f, 1.00f, 0.42f), 1.00f, 0.00f, 0.06f, false),
            new LiquidDef("tequila", "Tequila", "tequila", C(0.96f, 0.78f, 0.38f, 0.78f), 1.00f, 0.00f, 0.07f, false),
            new LiquidDef("rum", "Dark Rum", "rum", C(0.42f, 0.18f, 0.06f, 0.94f), 0.95f, 0.00f, 0.08f, false),
            new LiquidDef("gin", "Gin", "gin", C(0.76f, 0.95f, 0.86f, 0.45f), 1.00f, 0.00f, 0.06f, false),
            new LiquidDef("coffee", "Coffee Liqueur", "coffee", C(0.20f, 0.10f, 0.05f, 0.97f), 0.62f, 0.04f, 0.25f, true),
        };

        public static readonly SpoutDef[] Spouts = {
            new SpoutDef("slow", "Slow Pourer", "slow", 14f, 0.12f, 0.00f, 0.04f),
            new SpoutDef("speed", "Speed Pourer", "speed", 26f, 0.15f, 0.00f, 0.06f),
            new SpoutDef("fast", "Wide Pourer", "fast", 38f, 0.20f, 0.10f, 0.08f),
            new SpoutDef("open", "Open Neck", null, 48f, 0.35f, 0.40f, 0.12f),
        };

        public static readonly LevelDef[] Levels = {
            new LevelDef {
                name = "Opening Shift",
                blurb = "Slow pourers, simple glasses. Learn the feel.",
                orders = 5, secondsPerOrder = 25f, dim = 0f,
                glasses = new[] { "shot", "rocks" },
                spouts = new[] { "slow" },
                liquids = new[] { "whiskey", "tequila" },
                targetShots = new[] { 1f },
            },
            new LevelDef {
                name = "Happy Hour",
                blurb = "Half shots and singles. Faster spouts join in.",
                orders = 6, secondsPerOrder = 20f, dim = 0f,
                glasses = new[] { "shot", "shooter", "rocks", "highball" },
                spouts = new[] { "slow", "speed" },
                liquids = new[] { "whiskey", "tequila", "vodka", "rum" },
                targetShots = new[] { 0.5f, 1f, 1.5f },
            },
            new LevelDef {
                name = "Weekend Rush",
                blurb = "Tapered glasses and doubles. Watch that pint.",
                orders = 6, secondsPerOrder = 16f, dim = 0f,
                glasses = new[] { "shot", "shooter", "rocks", "highball", "pint", "martini" },
                spouts = new[] { "speed", "fast" },
                liquids = new[] { "whiskey", "tequila", "vodka", "rum", "gin" },
                targetShots = new[] { 0.5f, 1f, 1.5f, 2f },
            },
            new LevelDef {
                name = "Lost the Spouts",
                blurb = "Open necks glug and surge. Thick liquids lag.",
                orders = 7, secondsPerOrder = 14f, dim = 0.25f,
                glasses = new[] { "shot", "shooter", "rocks", "highball", "pint", "martini" },
                spouts = new[] { "speed", "fast", "open" },
                liquids = new[] { "whiskey", "tequila", "vodka", "rum", "gin", "coffee" },
                targetShots = new[] { 0.5f, 1f, 1.5f, 2f },
            },
            new LevelDef {
                name = "Last Call",
                blurb = "The lights are low. Listen to the pour.",
                orders = 8, secondsPerOrder = 12f, dim = 0.6f,
                glasses = new[] { "shot", "shooter", "rocks", "highball", "pint", "martini" },
                spouts = new[] { "slow", "speed", "fast", "open" },
                liquids = new[] { "whiskey", "tequila", "vodka", "rum", "gin", "coffee" },
                targetShots = new[] { 0.5f, 1f, 1.5f, 2f },
            },
        };

        public static LiquidDef Liquid(string id) => Array.Find(Liquids, l => l.id == id);
        public static SpoutDef Spout(string id) => Array.Find(Spouts, s => s.id == id);

        // Every glass Training mode can use.
        public static readonly string[] AllGlasses = { "shot", "shooter", "rocks", "highball", "pint", "martini" };

        public static readonly float[] AllTargets = { 0.5f, 1f, 1.5f, 2f };

        // ------------------------------------------------------------------ wording

        // "Half a shot", "1 shot", "1½ shots", "A double".
        public static string AmountText(float shots) {
            if (Mathf.Approximately(shots, 0.5f)) return "Half a shot";
            if (Mathf.Approximately(shots, 1f)) return "1 shot";
            if (Mathf.Approximately(shots, 1.5f)) return "1½ shots";
            if (Mathf.Approximately(shots, 2f)) return "A double";

            return shots.ToString("0.#") + " shots";
        }
    }
}
