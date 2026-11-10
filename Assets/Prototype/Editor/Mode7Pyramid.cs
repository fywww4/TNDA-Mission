using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TNDA.Prototype.Editor
{
    // Expand the existing Mode 7 scene, retaining the original cars, art, arena and Inspector tuning.
    public static class Mode7Pyramid
    {
        [MenuItem("TNDA/Mode 7/Apply eight-car pyramid")]
        public static void PatchScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode before applying the pyramid.");
            var scene = SceneManager.GetSceneByPath(Mode7Layout.ScenePath);
            bool opened = !scene.isLoaded;
            if (!opened && scene.isDirty) throw new InvalidOperationException("Save the Mode 7 scene before applying the pyramid.");
            if (opened) scene = EditorSceneManager.OpenScene(Mode7Layout.ScenePath, OpenSceneMode.Additive);
            try
            {
                var stage = scene.GetRootGameObjects().Select(g => g.GetComponentInChildren<Mode7Stage>(true)).First(s => s != null);
                var game = stage.game;
                Undo.RecordObject(game, "Eight-car pyramid");
                int oldCount = game.cars.Length;
                Array.Resize(ref game.cars, 8);
                Array.Resize(ref game.carLabels, 8);
                Array.Resize(ref game.healthBars, 8);
                Array.Resize(ref game.shieldViews, 8);
                Array.Resize(ref game.itemViews, 8);
                Array.Resize(ref game.scorePopups, 8);
                for (int i = oldCount; i < game.cars.Length; i++)
                {
                    var source = game.cars[i % oldCount];
                    var car = UnityEngine.Object.Instantiate(source, source.transform.parent);
                    Undo.RegisterCreatedObjectUndo(car.gameObject, "Eight-car pyramid");
                    car.name = "Car " + i;
                    car.index = i;
                    game.cars[i] = car;
                    game.healthBars[i] = car.transform.Find(AnimationUtility.CalculateTransformPath(game.healthBars[source.index], source.transform));
                    game.shieldViews[i] = car.transform.Find(AnimationUtility.CalculateTransformPath(game.shieldViews[source.index], source.transform));
                    game.itemViews[i] = car.transform.Find(AnimationUtility.CalculateTransformPath(game.itemViews[source.index].transform, source.transform)).GetComponent<SpriteRenderer>();
                    game.carLabels[i] = Copy(game.carLabels[source.index], "Car tag " + i);
                    game.scorePopups[i] = Copy(game.scorePopups[source.index], "Score pop-up " + i);
                }
                Undo.RecordObject(game.cars[0].transform.parent.gameObject, "Eight-car pyramid");
                game.cars[0].transform.parent.name = "Cars (8)";
                int oldSlots = game.rockets.Length;
                Array.Resize(ref game.rockets, 4);
                Array.Resize(ref game.explosions, 4);
                for (int i = oldSlots; i < game.rockets.Length; i++)
                {
                    var source = game.rockets[i % oldSlots];
                    game.rockets[i] = new ArcadeProjectile {
                        view = Copy(source.view, "Rocket " + i), slickView = Copy(source.slickView, "Oil slick " + i)
                    };
                    game.explosions[i] = Copy(game.explosions[i % oldSlots], "Rocket explosion " + i);
                }
                game.eliminationSchedule = new[] { new EliminationStep(40, 4), new EliminationStep(80, 2), new EliminationStep(120, 1) };
                Undo.RecordObject(stage, "Eight-car pyramid");
                stage.noticeAt1P = new Vector2Int(6, 346);
                stage.noticeAt2P = new Vector2Int(6, 194);
                EditorUtility.SetDirty(stage);
                ArcadeFontSetup.Apply(game);
                Mode7Layout.Apply(stage);
                game.PreparePreview();
                EditorUtility.SetDirty(game);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }

        private static T Copy<T>(T source, string name) where T : Component
        {
            var copy = UnityEngine.Object.Instantiate(source, source.transform.parent);
            Undo.RegisterCreatedObjectUndo(copy.gameObject, "Eight-car pyramid");
            copy.name = name;
            return copy;
        }
    }
}
