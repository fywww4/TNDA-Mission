using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TNDA.Prototype.Editor
{
    // Imports the user's debris sheet (Game3/Art/Debris.png, 4 cells of 64x64) and the big oil splat
    // (Game3/Art/OilSmear.png, 96x80, optional) and wires Mode7Debris into GAME3_Mode7.
    // Re-runnable; keeps Inspector values already set.
    public static class Mode7DebrisArt
    {
        private const string SheetPath = "Assets/Prototype/Game3/Art/Debris.png";
        private const string OilSmearPath = "Assets/Prototype/Game3/Art/OilSmear.png";

        [MenuItem("TNDA/Mode 7/Apply debris splash")]
        public static void Apply()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != "Assets/Scenes/GAME3_Mode7.unity")
                throw new System.InvalidOperationException("Open GAME3_Mode7 before applying the debris splash.");
            var stage = Object.FindAnyObjectByType<Mode7Stage>();
            if (stage == null) throw new System.InvalidOperationException("The scene needs Mode7Stage.");

            if (!(AssetImporter.GetAtPath(SheetPath) is TextureImporter importer))
                throw new System.InvalidOperationException("Missing " + SheetPath + " (the 4-cell debris sheet).");
            PixelArt(importer);
            importer.isReadable = true;
            importer.SaveAndReimport();
            // The big oil splat on a player's view (2026-11-21). Missing = the sheet's oil cell, enlarged.
            if (AssetImporter.GetAtPath(OilSmearPath) is TextureImporter oil)
            {
                PixelArt(oil);
                oil.SaveAndReimport();
            }

            var debris = stage.GetComponent<Mode7Debris>();
            if (debris == null) debris = stage.gameObject.AddComponent<Mode7Debris>();
            debris.stage = stage;
            debris.sheet = AssetDatabase.LoadAssetAtPath<Texture2D>(SheetPath);
            debris.oilSmear = AssetDatabase.LoadAssetAtPath<Texture2D>(OilSmearPath);
            EditorUtility.SetDirty(debris);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void PixelArt(TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Default;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
        }
    }
}
