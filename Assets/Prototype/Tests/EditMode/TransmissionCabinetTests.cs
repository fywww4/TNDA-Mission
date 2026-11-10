using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TNDA.Prototype.Tests
{
    // The real cabinet scene, through ArcadeGame's public setting/start API. Restore the user's preference afterwards.
    public class TransmissionCabinetTests
    {
        private Scene scene;
        private DemolitionGame game;
        private bool saved;
        private int value;

        [SetUp]
        public void OpenCabinet()
        {
            saved = PlayerPrefs.HasKey(ArcadeGame.TransmissionSaveKey);
            value = PlayerPrefs.GetInt(ArcadeGame.TransmissionSaveKey);
            scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/GAME3_Mode7.unity");
            game = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<DemolitionGame>(true)).Single();
        }

        [TearDown]
        public void RestorePreference()
        {
            EditorSceneManager.ClosePreviewScene(scene);
            if (saved) PlayerPrefs.SetInt(ArcadeGame.TransmissionSaveKey, value);
            else PlayerPrefs.DeleteKey(ArcadeGame.TransmissionSaveKey);
            PlayerPrefs.Save();
        }

        [Test]
        public void CabinetRemembersManualAcrossANewInstanceAndAppliesItToBothSeats()
        {
            game.SelectTransmission(true);
            EditorSceneManager.ClosePreviewScene(scene);
            scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/GAME3_Mode7.unity");
            game = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<DemolitionGame>(true)).Single();
            Assert.That(game.ManualTransmission, Is.True, "reopened cabinet reads the saved choice");
            game.StartRun(2);
            Assert.That(game.cars[0].Gear, Is.EqualTo(1));
            Assert.That(game.cars[1].Gear, Is.EqualTo(1));
            var shift = new ArcadeInput { shiftUpPressed = true };
            game.Advance(4, new ArcadeInput(), new ArcadeInput()); // finish the countdown
            game.Advance(.01f, shift, shift);
            Assert.That(game.cars[0].Gear, Is.EqualTo(2));
            Assert.That(game.cars[1].Gear, Is.EqualTo(2));
            game.SelectTransmission(false);
            Assert.That(game.ManualTransmission, Is.False);
        }
    }
}
