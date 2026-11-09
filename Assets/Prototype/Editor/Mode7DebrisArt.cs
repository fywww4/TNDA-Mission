using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TNDA.Prototype.Editor
{
    // Imports the user's debris sheet (Game3/Art/Debris.png, 4 cells of 64x64) and wires Mode7Debris into GAME3_Mode7.
    // Re-runnable; keeps Inspector values already set.
    public static class Mode7DebrisArt
    {
        private const string SheetPath = "Assets/Prototype/Game3/Art/Debris.png";

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
            importer.textureType = TextureImporterType.Default;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            // Mode7Debris copies the sheet with the body reds swapped for each victim's seat colour.
            importer.isReadable = true;
            importer.SaveAndReimport();

            var debris = stage.GetComponent<Mode7Debris>();
            if (debris == null) debris = stage.gameObject.AddComponent<Mode7Debris>();
            debris.stage = stage;
            debris.sheet = AssetDatabase.LoadAssetAtPath<Texture2D>(SheetPath);
            EditorUtility.SetDirty(debris);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
