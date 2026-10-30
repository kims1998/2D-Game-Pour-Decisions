using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PourDecisions.EditorTools {
    /*
    * Editor-only helpers:
    *  - Imports the generated art as crisp, uncompressed pixel sprites (point filtering, no mipmaps,
    *    no power-of-two scaling),
    *    so pixels and pivots match the art manifest in any project template.
    *  - Creates Assets/Scenes/PourDecisions.unity and adds it to Build Settings the first time the
    *    project opens. The game itself builds everything at runtime, so the scene can stay empty.
    */
    public class PourDecisionsArtImporter : AssetPostprocessor {
        const string ArtFolder = "Assets/PourDecisions/Resources/Art";

        // Bumping this re-runs the importer on every art file.
        public override uint GetVersion() => 4;

        void OnPreprocessTexture() {
            if (!assetPath.Replace('\\', '/').StartsWith(ArtFolder)) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.mipmapEnabled = false;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.alphaIsTransparency = true;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.filterMode = FilterMode.Point;  // keeps the retro pixels crisp
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.maxTextureSize = 4096;
        }
    }

    [InitializeOnLoad]
    public static class PourDecisionsSetup {
        const string ScenePath = "Assets/Scenes/PourDecisions.unity";

        static PourDecisionsSetup() {
            EditorApplication.delayCall += EnsureScene;
        }

        static void EnsureScene() {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (File.Exists(ScenePath)) return;
            CreateScene();
        }

        [MenuItem("Pour Decisions/Create Game Scene")]
        public static void CreateScene() {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Debug.Log("[Pour Decisions] Created " + ScenePath + " and added it to Build Settings. Press Play.");
        }

        [MenuItem("Pour Decisions/Reimport Art")]
        public static void ReimportArt() {
            AssetDatabase.ImportAsset("Assets/PourDecisions/Resources/Art", ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
        }
    }
}