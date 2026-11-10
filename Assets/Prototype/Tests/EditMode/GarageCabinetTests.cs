using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TNDA.Prototype.Tests
{
    public class GarageCabinetTests
    {
        private Scene scene;
        private DemolitionGame game;
        private readonly Dictionary<string, (bool exists, int value)> save = new Dictionary<string, (bool, int)>();

        [SetUp]
        public void OpenCabinetWithACleanIsolatedSave()
        {
            var keys = new[] { "TNDA.Gold", ArcadeGame.TransmissionSaveKey, "WreckBest" }
                .Concat(Enum.GetValues(typeof(VehicleAbility)).Cast<VehicleAbility>().Select(a => "TNDA.Upgrades." + a))
                .Concat(Challenges.All.SelectMany(c => new[] { "TNDA.Challenge.Best." + c.Id, "TNDA.Challenge.Unlocked." + c.Id }));
            foreach (string key in keys) { save[key] = (PlayerPrefs.HasKey(key), PlayerPrefs.GetInt(key)); PlayerPrefs.DeleteKey(key); }
            PlayerPrefs.Save();
            Open();
        }

        private void Open()
        {
            scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/GAME3_Mode7.unity");
            game = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<DemolitionGame>(true)).Single();
            game.PreparePreview();
        }

        private void FundCabinet()
        {
            EditorSceneManager.ClosePreviewScene(scene);
            new GoldWallet().AwardRound((1, 65000));
            Open();
        }

        [TearDown]
        public void RestoreUsersSave()
        {
            EditorSceneManager.ClosePreviewScene(scene);
            foreach (var entry in save)
                if (entry.Value.exists) PlayerPrefs.SetInt(entry.Key, entry.Value.value); else PlayerPrefs.DeleteKey(entry.Key);
            save.Clear();
            PlayerPrefs.Save();
        }

        [Test]
        public void TitleOpensGarageAndReturningKeepsModeAndTransmission()
        {
            Assert.That(game.garage, Is.Not.Null, "saved scene must wire the garage");
            game.SelectMode(CabinetMode.Challenge);
            game.SelectTransmission(true);
            game.OpenGarage();
            Assert.That(game.Phase, Is.EqualTo(ArcadePhase.Garage));
            Assert.That(game.garage.gameObject.activeSelf, Is.True);
            Assert.That(game.attractScreen.activeSelf, Is.False);
            Assert.That(game.TryBuySelectedUpgrade(), Is.False);
            game.ShowAttract();
            game.Advance(0, new ArcadeInput(), new ArcadeInput());
            Assert.That(game.garage.gameObject.activeSelf, Is.False);
            Assert.That(game.attractScreen.activeSelf, Is.True);
            Assert.That(game.Mode, Is.EqualTo(CabinetMode.Challenge));
            Assert.That(game.ManualTransmission, Is.True);
        }

        [Test]
        public void GaragePurchaseReloadAndLongDeleteResetUseTheSameSavedUpgrades()
        {
            FundCabinet();
            game.OpenGarage();
            Assert.That(game.TryBuySelectedUpgrade(), Is.True);
            Assert.That(game.Gold, Is.EqualTo(700));
            EditorSceneManager.ClosePreviewScene(scene);
            Open();
            Assert.That(game.Upgrades.Level(VehicleAbility.TopSpeed), Is.EqualTo(1));
            game.Advance(3.01f, new ArcadeInput { resetSaveHeld = true }, new ArcadeInput());
            Assert.That(game.Gold, Is.Zero);
            Assert.That(game.Upgrades.Level(VehicleAbility.TopSpeed), Is.Zero);
            Assert.That(new VehicleUpgrades().Level(VehicleAbility.TopSpeed), Is.Zero);
        }

        [TestCase(1, false)]
        [TestCase(2, false)]
        [TestCase(1, true)]
        public void EveryHumanSeatInArcadeAndChallengeReceivesTheSharedDrivingUpgrade(int players, bool challenge)
        {
            FundCabinet();
            game.OpenGarage();
            for (int i = 0; i < 4; i++) Assert.That(game.TryBuySelectedUpgrade(), Is.True);
            game.StartRun(players, challenge ? Challenges.All[0] : null);
            game.Advance(4, new ArcadeInput(), new ArcadeInput());
            var gas = new ArcadeInput { throttle = 1 };
            for (int f = 0; f < 300; f++)
            {
                for (int i = 0; i < game.cars.Length; i++) { game.cars[i].position = new Vector2(-4 + i, 0); game.cars[i].heading = 90; }
                foreach (var prop in game.props) { prop.broken = true; prop.respawn = 100; }
                game.Advance(1f / 60, gas, gas);
            }
            for (int i = 0; i < players; i++) Assert.That(game.cars[i].speed, Is.EqualTo(3.36f).Within(.001f), "seat " + i);
            Assert.That(game.driveSpeed, Is.EqualTo(3.5f), "base speed stays unchanged");
        }
    }
}
