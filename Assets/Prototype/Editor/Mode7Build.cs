using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TNDA.Prototype.Editor
{
    public static class Mode7Build
    {
        private const string MaterialPath = "Assets/Prototype/Game3/Art/Mode7Ground.mat";

        [MenuItem("TNDA/Mode 7/Promote to main scene")]
        public static void Promote()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode before promoting Mode 7.");
            var scene = SceneManager.GetSceneByPath(Mode7Layout.ScenePath);
            bool opened = !scene.isLoaded;
            if (!opened && scene.isDirty) throw new InvalidOperationException("Save the Mode 7 scene before promoting it.");
            if (opened) scene = EditorSceneManager.OpenScene(Mode7Layout.ScenePath, OpenSceneMode.Additive);
            try
            {
                var stage = scene.GetRootGameObjects().Select(g => g.GetComponentInChildren<Mode7Stage>(true)).First(s => s != null);
                var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
                if (material == null)
                {
                    material = new Material(Shader.Find("TNDA/Mode7 Ground"));
                    AssetDatabase.CreateAsset(material, MaterialPath);
                }
                Undo.RecordObject(stage, "Mode 7 ground material");
                stage.groundMaterial = material;
                EditorUtility.SetDirty(stage);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(Mode7Layout.ScenePath, true) };
                AssetDatabase.SaveAssets();
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
