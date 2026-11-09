using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TNDA.Prototype.Tests
{
    public class Mode7RenderingTests
    {
        [UnitySetUp]
        public IEnumerator LoadStage()
        {
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/Scenes/GAME3_Mode7.unity", new LoadSceneParameters(LoadSceneMode.Single));
#else
            yield return SceneManager.LoadSceneAsync("GAME3_Mode7");
#endif
            yield return null;
        }

        [UnityTest]
        public IEnumerator EachViewDrawsTheNearerStandingObjectInFrontAndTheMapRestoresFlatPositions()
        {
            var stage = Object.FindAnyObjectByType<Mode7Stage>();
            var game = stage.game;
            game.enabled = false;
            game.StartRun(2);
            stage.carFrames = null;
            stage.carLift = stage.propLift = stage.pickupLift = stage.itemLift = stage.crownLift = stage.explosionLift = .5f;
            stage.crownGap = -100; // Keep the crown at its minimum fixture height while testing overlap.
            yield return null; // Let the stage collect these ground offsets before rendering either view.
            var car = game.cars[0];
            var subjects = new[] {
                (root: game.pickups[0].view, view: game.pickups[0].view.GetComponentInChildren<SpriteRenderer>(true)),
                (root: game.props[0].view, view: game.props[0].view.GetComponentInChildren<SpriteRenderer>(true)),
                (root: game.cars[1].transform, view: game.cars[1].body),
                (root: game.itemViews[0].transform, view: game.itemViews[0]),
                (root: game.crownView.transform, view: game.crownView),
                (root: game.explosions[0].transform, view: game.explosions[0]) };
            var texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            var sprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.one * .5f, 1);
            var material = new Material(Shader.Find("Sprites/Default"));
            var target = new RenderTexture(64, 64, 24);
            var readback = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;
            var cameras = new[] { GameObject.Find("Mode7 chase P1").GetComponent<Camera>(), GameObject.Find("Mode7 chase P2").GetComponent<Camera>() };
            var originalTargets = new[] { cameras[0].targetTexture, cameras[1].targetTexture };
            try
            {
                foreach (var subject in subjects)
                {
                    foreach (var renderer in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include)) renderer.enabled = false;
                    car.heading = 90;
                    car.transform.position = new Vector3(0, -1, 0);
                    car.transform.localScale = Vector3.one;
                    car.body.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                    car.body.transform.localScale = Vector3.one;
                    subject.root.gameObject.SetActive(true);
                    subject.root.position = new Vector3(0, subject.view == game.crownView ? 1 + game.crownHeight : 1, 0);
                    subject.root.localScale = Vector3.one;
                    if (subject.view.transform != subject.root) subject.view.transform.localPosition = Vector3.zero;
                    subject.view.transform.localScale = Vector3.one;
                    yield return null;
                    foreach (var view in new[] { car.body, subject.view })
                    {
                        view.gameObject.SetActive(true);
                        view.enabled = true;
                        view.sprite = sprite;
                        view.sharedMaterial = material;
                        view.SetPropertyBlock(null);
                    }
                    car.body.color = Color.cyan;
                    subject.view.color = Color.white;
                    car.body.sortingOrder = 10;
                    subject.view.sortingOrder = 4;
                    for (int seat = 0; seat < 2; seat++)
                    {
                        var camera = cameras[seat];
                        camera.targetTexture = target;
                        camera.rect = new Rect(0, 0, 1, 1);
                        camera.transform.SetPositionAndRotation(new Vector3(0, seat == 0 ? -4 : 4, -.5f),
                            Quaternion.LookRotation(seat == 0 ? Vector3.up : Vector3.down, Vector3.back));
                        camera.Render();
                        RenderTexture.active = target;
                        readback.ReadPixels(new Rect(0, 0, 64, 64), 0, 0);
                        readback.Apply();
                        Color expected = seat == 0 ? Color.cyan : Color.white;
                        Assert.That(Vector4.Distance(readback.GetPixel(32, 32), expected), Is.LessThan(.05f),
                            subject.root.name + ": nearer sprite must win in seat " + seat + ", actual " + readback.GetPixel(32, 32));
                        Assert.That(Vector2.Distance(subject.root.position, Vector2.up), Is.LessThan(.001f),
                            "Switching views must preserve the object's ground position, including held items parented to a car.");
                    }
                    game.sceneCamera.Render();
                    Assert.That(subject.root.position.z, Is.EqualTo(0).Within(.001f));
                    Assert.That(subject.view.sortingOrder, Is.EqualTo(4));
                }
                // Keep a real scene floor marking visible: it must stay behind the nearer car, even though its
                // old top-down layer order is higher than the default order of a newly created SortingGroup.
                SpriteRenderer marking = null;
                foreach (var renderer in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include))
                {
                    renderer.enabled = false;
                    if (renderer.name == "Floor marking" && renderer is SpriteRenderer floor) marking = floor;
                }
                Assert.That(marking, Is.Not.Null);
                marking.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                marking.transform.localScale = Vector3.one * 100;
                foreach (var view in new[] { marking, car.body })
                {
                    view.enabled = true;
                    view.sprite = sprite;
                    view.sharedMaterial = material;
                    view.SetPropertyBlock(null);
                }
                marking.color = Color.white;
                car.body.color = Color.cyan;
                cameras[0].transform.SetPositionAndRotation(new Vector3(0, -4, -2),
                    Quaternion.LookRotation(new Vector3(0, 3, 1.5f), Vector3.back));
                cameras[0].Render();
                RenderTexture.active = target;
                readback.ReadPixels(new Rect(0, 0, 64, 64), 0, 0);
                readback.Apply();
                Assert.That(Vector4.Distance(readback.GetPixel(32, 32), Color.cyan), Is.LessThan(.05f),
                    "A floor marking behind the car must not show through its body.");
            }
            finally
            {
                RenderTexture.active = previous;
                for (int i = 0; i < cameras.Length; i++) cameras[i].targetTexture = originalTargets[i];
                Object.DestroyImmediate(readback);
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(sprite);
                Object.DestroyImmediate(texture);
            }
        }
    }
}
