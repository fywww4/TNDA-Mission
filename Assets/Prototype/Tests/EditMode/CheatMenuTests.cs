using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TNDA.Prototype.Tests
{
    // The ~ cheat menu on the real Mode 7 scene: gold and stage clears work anywhere, score and the enemy wipe only in
    // a live match, and invincibility stops all damage to players. The user's save and the static switch are restored.
    public class CheatMenuTests
    {
        private const float Dt = .05f;
        private Scene scene;
        private DemolitionGame game;
        private readonly Dictionary<string, (bool exists, int value)> save = new Dictionary<string, (bool, int)>();

        [SetUp]
        public void OpenTheGameWithAnIsolatedSave()
        {
            var keys = new[] { "TNDA.Gold", "WreckBest" }
                .Concat(Campaign.Stages.Select(s => "TNDA.Campaign.Stage" + s.Number + ".Cleared"))
                .Concat(Enum.GetValues(typeof(VehicleAbility)).Cast<VehicleAbility>().Select(a => "TNDA.Upgrades." + a));
            foreach (string key in keys) { save[key] = (PlayerPrefs.HasKey(key), PlayerPrefs.GetInt(key)); PlayerPrefs.DeleteKey(key); }
            Cheats.Invincible = false;
            scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/GAME3_Mode7.unity");
            game = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<DemolitionGame>(true)).Single();
            game.PreparePreview();
        }

        [TearDown]
        public void RestoreUsersSave()
        {
            Cheats.Invincible = false;
            EditorSceneManager.ClosePreviewScene(scene);
            foreach (var entry in save)
                if (entry.Value.exists) PlayerPrefs.SetInt(entry.Key, entry.Value.value); else PlayerPrefs.DeleteKey(entry.Key);
            save.Clear();
            PlayerPrefs.Save();
        }

        private void Run(float seconds)
        {
            for (float t = 0; t < seconds; t += Dt) game.Advance(Dt, new ArcadeInput(), new ArcadeInput());
        }

        private void StartOnePlayerMatch()
        {
            game.SelectMode(CabinetMode.Normal);
            game.StartRun(1);
            while (game.Phase == ArcadePhase.Countdown) Run(Dt);
            Assert.That(game.Phase, Is.EqualTo(ArcadePhase.Playing));
        }

        [Test]
        public void GoldAndStageClearsWorkOnTheTitle()
        {
            int before = game.Gold;
            game.UseCheat(1);
            Assert.That(game.Gold, Is.EqualTo(before + Cheats.Gold));
            game.UseCheat(2);
            Assert.That(Campaign.Stages.All(s => game.Progress.IsCleared(s) && game.Progress.IsUnlocked(s)), Is.True);
        }

        [Test]
        public void ScoreAndWipeDoNothingOutsideAMatch()
        {
            float score = game.cars[0].score;
            game.UseCheat(3);
            game.UseCheat(4);
            Assert.That(game.Phase, Is.EqualTo(ArcadePhase.Attract));
            Assert.That(game.cars[0].score, Is.EqualTo(score));
        }

        [Test]
        public void ScoreCheatAddsToPlayerOne()
        {
            StartOnePlayerMatch();
            float score = game.cars[0].score;
            game.UseCheat(3);
            Assert.That(game.cars[0].score, Is.EqualTo(score + Cheats.Score));
        }

        [Test]
        public void WipingTheEnemiesCrownsTheSinglePlayer()
        {
            StartOnePlayerMatch();
            game.UseCheat(4);
            Assert.That(game.cars.Where(c => c.computer).All(c => c.eliminated), Is.True);
            Assert.That(game.Champion, Is.EqualTo(0));
        }

        [Test]
        public void InvincibilityStopsPlayerDamageButNotComputerDamage()
        {
            StartOnePlayerMatch();
            Run(2.5f);
            foreach (var car in game.cars) { car.spawnProtection = 0; car.shieldSeconds = 0; }
            game.UseCheat(5);
            Assert.That(Cheats.Invincible, Is.True);
            float health = game.cars[0].health;
            game.DamageVehicle(game.cars[0], 99);
            Assert.That(game.cars[0].alive, Is.True);
            Assert.That(game.cars[0].health, Is.EqualTo(health));
            var computer = game.cars.First(c => c.computer && c.alive);
            game.DamageVehicle(computer, 99);
            Assert.That(computer.alive, Is.False);
            game.UseCheat(5);
            Assert.That(Cheats.Invincible, Is.False);
        }

        [Test]
        public void TheMenuOpensAndClosesWithoutChangingThePhase()
        {
            StartOnePlayerMatch();
            game.SetCheatMenu(true);
            Assert.That(game.CheatMenuOpen, Is.True);
            game.SetCheatMenu(false);
            Assert.That(game.CheatMenuOpen, Is.False);
            Assert.That(game.Phase, Is.EqualTo(ArcadePhase.Playing));
        }
    }
}
