using System.IO;
using UnityEditor;
using UnityEngine;

namespace PourDecisions.EditorTools {
    /*
    * Menu: Pour Decisions > Build Glass and Bottle Prefabs.
    * Builds a prefab for every glass, bottle and customer that does not have one yet, using the
    * pivots and sizes from the art manifest. Existing prefabs are left alone. Everything lands in
    * Assets/PourDecisions/Prefabs.
    */
    public static class BuildPrefabs {
        const string ArtFolder = "Assets/PourDecisions/Resources/Art/";
        const string PrefabFolder = "Assets/PourDecisions/Prefabs/";
        const string ManifestPath = "Assets/PourDecisions/Resources/Data/art_manifest.json";
        const float ArtPpu = 140f;
        const int CustomerCount = 6;
        const float CustomerPpu = 100f;

        [MenuItem("Pour Decisions/Build Glass and Bottle Prefabs")]
        public static void Build() {
            var manifestText = AssetDatabase.LoadAssetAtPath<TextAsset>(ManifestPath);

            if (manifestText == null) {
                Debug.LogError("[Pour Decisions] Could not find " + ManifestPath);
                return;
            }

            var manifest = JsonUtility.FromJson<ArtManifest>(manifestText.text);
            Directory.CreateDirectory(PrefabFolder);

            // Step 1: import settings. PPU 140 and the pivot from the manifest, for every sprite we use.
            foreach (var g in manifest.glasses) {
                SetImport(ArtFolder + "glass_" + g.id + "_back.png", g.pivotX, g.pivotY);
                SetImport(ArtFolder + "glass_" + g.id + "_front.png", g.pivotX, g.pivotY);
            }

            foreach (var b in manifest.bottles) {
                SetImport(ArtFolder + "bottle_" + b.id + "_back.png", b.pivotX, b.pivotY);
                SetImport(ArtFolder + "bottle_" + b.id + "_front.png", b.pivotX, b.pivotY);
            }

            // Customers: PPU 100, pivot at the feet (bottom center).
            for (int i = 0; i < CustomerCount; i++) {
                foreach (var mood in new[] { "happy", "neutral", "upset" }) {
                    SetImport(ArtFolder + "customer_" + i + "_" + mood + ".png", 0.5f, 0f, CustomerPpu);
                }
            }

            foreach (var s in manifest.spouts) SetImport(ArtFolder + "spout_" + s.id + ".png", 0.5f, s.pivotY);
            AssetDatabase.Refresh();

            // Step 2: the prefabs.
            int made = 0, skipped = 0;
            foreach (var g in manifest.glasses) {
                if (MakeGlass(g)) made++; else skipped++;
            }

            foreach (var b in manifest.bottles) {
                if (MakeBottle(b)) made++; else skipped++;
            }

            for (int i = 0; i < CustomerCount; i++) {
                if (MakeCustomer(i)) made++; else skipped++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[Pour Decisions] Built " + made + " prefabs, skipped " + skipped + " that already existed.");
        }

        // Sets PPU and a custom pivot on one sprite file.
        static void SetImport(string path, float pivotX, float pivotY, float ppu = ArtPpu) {
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;

            if (ti == null) {
                Debug.LogWarning("[Pour Decisions] Missing art file " + path);
                return;
            }

            var settings = new TextureImporterSettings();
            ti.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = new Vector2(pivotX, pivotY);
            settings.spritePixelsPerUnit = ppu;
            ti.SetTextureSettings(settings);
            ti.SaveAndReimport();
        }

        // Empty parent at the glass spot with a Back (order 0) and a Front (order 5) sprite.
        static bool MakeGlass(GlassDef g) {
            string path = PrefabFolder + "Glass_" + Title(g.id) + ".prefab";
            if (File.Exists(path)) return false;
            var root = new GameObject("Glass_" + Title(g.id));
            root.transform.position = new Vector3(K.GlassX, K.CounterY, 0f);
            AddSprite(root.transform, "Back", ArtFolder + "glass_" + g.id + "_back.png", Vector3.zero, 0);
            AddSprite(root.transform, "Front", ArtFolder + "glass_" + g.id + "_front.png", Vector3.zero, 5);
            Save(root, path);

            return true;
        }

        // Parent scaled by 1.15 and placed so its base rests on the counter; three sprite children.
        static bool MakeBottle(BottleDef b) {
            string path = PrefabFolder + "Bottle_" + Title(b.id) + ".prefab";
            if (File.Exists(path)) return false;
            var root = new GameObject("Bottle_" + Title(b.id));
            float restY = K.CounterY - b.bodyBottomY * K.BottleScale;
            root.transform.position = new Vector3(K.GlassX - 4.3f, restY, 0f);
            root.transform.localScale = new Vector3(K.BottleScale, K.BottleScale, 1f);
            AddSprite(root.transform, "Back", ArtFolder + "bottle_" + b.id + "_back.png", Vector3.zero, 10);
            AddSprite(root.transform, "Front", ArtFolder + "bottle_" + b.id + "_front.png", Vector3.zero, 12);
            AddSprite(root.transform, "Spout", ArtFolder + "spout_speed.png", new Vector3(0f, b.neckTopY - 0.06f, 0f), 13);
            Save(root, path);
            
            return true;
        }

        // One customer standing behind the counter, off to the right where they walk in from.
        static bool MakeCustomer(int index) {
            string path = PrefabFolder + "Customer_" + index + ".prefab";
            if (File.Exists(path)) return false;
            var root = new GameObject("Customer_" + index);
            root.transform.position = new Vector3(7f, -2.85f, 0f);
            var sr = root.AddComponent<SpriteRenderer>();
            sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtFolder + "customer_" + index + "_neutral.png");
            sr.sortingOrder = -60;
            Save(root, path);

            return true;
        }

        static void AddSprite(Transform parent, string name, string spritePath, Vector3 localPos, int order) {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            sr.sortingOrder = order;
        }

        static void Save(GameObject root, string path) {
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
        }

        // "whiskey" becomes "Whiskey" so names match the existing prefabs.
        static string Title(string id) {
            return char.ToUpper(id[0]) + id.Substring(1);
        }
    }
}