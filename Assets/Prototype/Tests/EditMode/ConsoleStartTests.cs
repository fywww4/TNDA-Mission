using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TNDA.Prototype.Tests
{
    // The console flow (ticket 17): START opens the 1P/2P menu and starts the chosen count, Esc backs out, and the
    // results wait for START (same game again) instead of returning to the title on a timer.
    public class ConsoleStartTests
    {
        private const float Dt = .05f;
        private Scene scene;
        private DemolitionGame game;
        private readonly Dictionary<string, (bool exists, int value)> save = new Dictionary<string, (bool, int)>();

        [SetUp]
        public void OpenTheGameWithAnIsolatedSave()
        {
            var keys = new[] { "TNDA.Gold", ArcadeGame.TransmissionSaveKey, "WreckBest" }
                .Concat(Enum.GetValues(typeof(VehicleAbility)).Cast<VehicleAbility>().Select(a => "TNDA.Upgrades." + a));
            foreach (string key in keys) { save[key] = (PlayerPrefs.HasKey(key), PlayerPrefs.GetInt(key)); PlayerPrefs.DeleteKey(key); }
            scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/GAME3_Mode7.unity");
            game = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<DemolitionGame>(true)).Single();
            game.PreparePreview();
            game.SelectMode(CabinetMode.Normal);
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

        // Idle humans would be knocked out at 40 s (and GAME OVER returns to the title), so P1 is kept far ahead to
        // reach the results through the champion's victory picture.
        private void Run(float seconds, bool p1Leads = false)
        {
            for (float t = 0; t < seconds; t += Dt)
            {
                if (p1Leads && !game.cars[0].eliminated) game.cars[0].score = 99999;
                game.Advance(Dt, new ArcadeInput(), new ArcadeInput());
            }
        }

        [Test]
        public void StartOpensThePlayerMenuAndTheSecondStartBeginsTheChosenCount()
        {
            game.PressStart();
            Assert.That(game.Phase, Is.EqualTo(ArcadePhase.Attract));
            Assert.That(game.ChoosingPlayers, Is.True);
            Assert.That(game.PlayerChoice, Is.EqualTo(1));

            game.PickPlayers(2);
            game.PressStart();

            Assert.That(game.Phase, Is.EqualTo(ArcadePhase.Countdown));
            Assert.That(game.PlayerCount, Is.EqualTo(2));
            Assert.That(game.ChoosingPlayers, Is.False);
        }

        [Test]
        public void BackClosesTheMenuFirstAndEndsAMatchForTheTitle()
        {
            game.PressStart();
            game.Back();
            Assert.That(game.ChoosingPlayers, Is.False);
            Assert.That(game.Phase, Is.EqualTo(ArcadePhase.Attract));

            game.PressStart();
            game.PressStart();
            Assert.That(game.Phase, Is.EqualTo(ArcadePhase.Countdown));
            Assert.That(game.PlayerCount, Is.EqualTo(1));
            game.Back();
            Assert.That(game.Phase, Is.EqualTo(ArcadePhase.Attract));
        }

        [Test]
        public void TheResultsWaitForStartWhichReplaysTheSamePlayerCount()
        {
            game.PressStart();
            game.PickPlayers(2);
            game.PressStart();
            Run(game.countdownSeconds + 130, p1Leads: true);
            Assert.That(game.Phase, Is.EqualTo(ArcadePhase.Results), "P1 wins the 2P match and the results show");

            Run(60);
            Assert.That(game.Phase, Is.EqualTo(ArcadePhase.Results), "no timer sends the results back to the title");

            game.PressStart();
            Assert.That(game.Phase, Is.EqualTo(ArcadePhase.Countdown));
            Assert.That(game.PlayerCount, Is.EqualTo(2));
        }
    }
}
