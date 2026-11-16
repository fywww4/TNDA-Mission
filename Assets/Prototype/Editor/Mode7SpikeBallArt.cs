using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TNDA.Prototype.Editor
{
    // Ticket 21: imports SpikeBall.png (128x32, four 32x32 rolling frames from Art/Source/SpikeBallSource.png) and puts
    // two spike balls into GAME3_Mode7. Safe to rerun: an existing ball layout is kept.
    public static class Mode7SpikeBallArt
    {
        private const string SheetPath = "Assets/Prototype/Game3/Art/SpikeBall.png";

        [MenuItem("TNDA/Mode 7/Apply spike balls")]
        public static void Apply()
        {
            var stage = Object.FindAnyObjectByType<Mode7Stage>();
            if (stage == null || stage.gameObject.scene.path != "Assets/Scenes/GAME3_Mode7.unity")
                throw new System.InvalidOperationException("Open GAME3_Mode7 before applying spike balls.");
            AssetDatabase.ImportAsset(SheetPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(SheetPath);
            importer.textureType = TextureImporterType.Default;
            importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Clamp; importer.npotScale = TextureImporterNPOTScale.None;
            importer.alphaIsTransparency = true; importer.SaveAndReimport();
            var game = stage.game;
            game.spikeBallSheet = AssetDatabase.LoadAssetAtPath<Texture2D>(SheetPath);
            if (game.spikeBalls == null || game.spikeBalls.Length == 0)
            {
                Vector2 centre = (game.arenaMin + game.arenaMax) / 2;
                // Used only while random spots are off: between the walls, clear of the fences and both start rows.
                game.spikeBalls = new[] {
                    new SpikeBall { point = centre + new Vector2(-1.5f, -.8f), heading = 30 },
                    new SpikeBall { point = centre + new Vector2(1.5f, .8f), heading = 210 }
                };
            }
            EditorUtility.SetDirty(game);
            EditorSceneManager.MarkSceneDirty(stage.gameObject.scene);
            EditorSceneManager.SaveScene(stage.gameObject.scene);
        }
    }
}
