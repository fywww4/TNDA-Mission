using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace TNDA.Prototype.Tests
{
    public class Mode7MinimapTests
    {
        private Mode7Stage stage;

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
            stage = Object.FindAnyObjectByType<Mode7Stage>();
            stage.game.enabled = false;
            stage.game.StartRun(2);
            yield return null;
        }

        [UnityTest]
        public IEnumerator AllEightCarsShowPixelArrowsInSixteenWorldDirections()
        {
            var frames = new HashSet<Sprite>();
            for (int direction = 0; direction < 16; direction++)
            {
                foreach (var car in stage.game.cars) car.heading = direction * 22.5f;
                yield return null;
                stage.game.sceneCamera.Render();
                foreach (var car in stage.game.cars)
                {
                    var arrow = car.transform.Find("Minimap blip").GetComponent<SpriteRenderer>();
                    frames.Add(arrow.sprite);
                    Assert.That(arrow.color, Is.EqualTo(car.body.color), car.DriverName + " keeps its seat colour");
                    Assert.That(Quaternion.Angle(arrow.transform.rotation, Quaternion.identity), Is.LessThan(.01f),
                        "Use baked pixel directions, including after chase-camera billboarding");
                    float worldPerPixel = stage.game.sceneCamera.orthographicSize * 2 / stage.minimapRect.height;
                    Assert.That(arrow.bounds.size.x / worldPerPixel, Is.EqualTo(7).Within(.001f), "Each arrow texel is one map pixel");
                    var texture = arrow.sprite.texture;
                    Assert.That(texture.GetPixel(0, 0).a, Is.EqualTo(0), "The square must become an arrow");
                    if (direction == 0)
                    {
                        Assert.That(texture.GetPixel(texture.width - 1, texture.height / 2).a, Is.EqualTo(1), "East-facing tip");
                        Assert.That(texture.GetPixel(texture.width - 1, 0).a, Is.EqualTo(0));
                    }
                }
            }
            Assert.That(stage.game.cars.Length, Is.EqualTo(8));
            Assert.That(frames.Count, Is.EqualTo(16), "All sixteen headings have distinct pixel frames");
            var first = stage.game.cars[0];
            first.heading = -1;
            yield return null;
            stage.game.sceneCamera.Render();
            var wrapped = first.transform.Find("Minimap blip").GetComponent<SpriteRenderer>().sprite;
            first.heading = 10;
            yield return null;
            stage.game.sceneCamera.Render();
            Assert.That(first.transform.Find("Minimap blip").GetComponent<SpriteRenderer>().sprite, Is.SameAs(wrapped),
                "Headings either side of zero snap to east");
        }

        [UnityTest]
        public IEnumerator MinimapHasItsOwnCoarseTextureAboveBothDebrisLayersAndBelowHud()
        {
            stage.minimapWithStandingsOnly = false;
            foreach (int players in new[] { 1, 2 })
            {
                stage.game.StartRun(players);
                yield return null;
                var map = stage.game.sceneCamera;
                Assert.That(map.targetTexture.width, Is.EqualTo(stage.minimapRect.width));
                Assert.That(map.targetTexture.height, Is.EqualTo(stage.minimapRect.height));
                Assert.That(map.targetTexture.filterMode, Is.EqualTo(FilterMode.Point));
                Assert.That(map.rect, Is.EqualTo(new Rect(0, 0, 1, 1)));
                var cabinet = stage.game.header.transform.parent;
                var image = cabinet.GetComponentsInChildren<RawImage>(true).Single(i => i.texture == map.targetTexture);
                Assert.That(image.rectTransform.anchoredPosition, Is.EqualTo((Vector2)stage.minimapRect.position * 2));
                Assert.That(image.rectTransform.sizeDelta, Is.EqualTo((Vector2)stage.minimapRect.size * 2));
                foreach (var debris in cabinet.Cast<Transform>().Where(t => t.name.StartsWith("Mode7 debris")))
                    Assert.That(image.transform.GetSiblingIndex(), Is.GreaterThan(debris.GetSiblingIndex()), "Map covers debris in either half");
                Assert.That(image.transform.GetSiblingIndex(), Is.LessThan(stage.game.header.transform.GetSiblingIndex()), "HUD stays on top");
                Assert.That(image.GetComponentsInChildren<Image>().Length, Is.EqualTo(4), "Keep all four white frame edges above debris");
                foreach (var edge in image.GetComponentsInChildren<Image>()) Assert.That(edge.color, Is.EqualTo(ArcadeGame.White));
                stage.minimap = false;
                yield return null;
                Assert.That(image.gameObject.activeSelf, Is.False, "The map toggle hides its frame too");
                stage.minimap = true;
                yield return null;
                Assert.That(image.gameObject.activeSelf, Is.True);
                stage.minimapWithStandingsOnly = true;
                yield return null;
                Assert.That(stage.game.standingsHud.TableShown, Is.False);
                Assert.That(image.gameObject.activeSelf, Is.False, "Without SELECT held the map stays hidden");
                Assert.That(map.enabled, Is.False, "and its camera stops drawing");
                stage.minimapWithStandingsOnly = false;
            }
            stage.minimapWithStandingsOnly = true;
        }

        [UnityTest]
        public IEnumerator SpinningEliminatedCarsKeepTurningTheirArrowUntilHiddenAndStageCanRestart()
        {
            var car = stage.game.cars[0];
            car.heading = 90;
            car.Sync();
            yield return null;
            stage.game.sceneCamera.Render();
            var arrow = car.transform.Find("Minimap blip").GetComponent<SpriteRenderer>();
            var north = arrow.sprite;
            var rules = new VehicleLifecycleRules { spinStartSpeed = 4, spinAcceleration = 14, spinTurnsPerSecond = 4 };
            car.Eliminate(8, rules);
            car.AdvanceSpinOut(1f / 64, rules, Vector2.zero, 20);
            car.Sync();
            yield return null;
            stage.game.sceneCamera.Render();
            Assert.That(car.Visible, Is.True);
            Assert.That(arrow.gameObject.activeInHierarchy, Is.True, "An eliminated car still has an arrow during its exit");
            Assert.That(arrow.sprite, Is.Not.SameAs(north), "The arrow follows the spinning heading");
            car.AdvanceSpinOut(10, rules, Vector2.zero, 20);
            stage.game.Advance(0, default, default);
            Assert.That(arrow.gameObject.activeInHierarchy, Is.False, "The arrow goes away with the car");
            stage.enabled = false;
            yield return null;
            Assert.That(stage.game.sceneCamera.targetTexture, Is.Null, "Release the minimap render target");
            Assert.That(car.transform.Find("Minimap blip"), Is.Null);
            stage.enabled = true;
            stage.game.StartRun(2);
            yield return null;
            Assert.That(stage.game.cars.Sum(c => c.GetComponentsInChildren<SpriteRenderer>(true).Count(r => r.name == "Minimap blip")), Is.EqualTo(8));
            var cabinet = stage.game.header.transform.parent;
            var image = cabinet.GetComponentsInChildren<RawImage>(true).Single(i => i.texture == stage.game.sceneCamera.targetTexture);
            foreach (var debris in cabinet.Cast<Transform>().Where(t => t.name.StartsWith("Mode7 debris")))
                Assert.That(image.transform.GetSiblingIndex(), Is.GreaterThan(debris.GetSiblingIndex()));
        }
    }
}
