using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TNDA.Prototype.Tests
{
    // Campaign stage 3's own arena and cars (ticket 25), on the real Mode 7 scene: the night derby's terrain, the
    // minions' checker pattern, the boss's own sheet; a normal match keeps the usual arena.
    public class BossArenaTests
    {
        private const float Dt = .05f;
        private Scene scene;
        private DemolitionGame game;
        private readonly Dictionary<string, (bool exists, int value)> save = new Dictionary<string, (bool, int)>();

        [SetUp]
        public void OpenTheGameWithAnIsolatedSave()
        {
            var keys = new[] { "TNDA.Gold", "WreckBest", "TNDA.Decals.P1", "TNDA.Decals.P2" }
                .Concat(Enum.GetValues(typeof(CarDecal)).Cast<CarDecal>().Select(d => "TNDA.Decals.Owned." + d))
                .Concat(Campaign.Stages.Select(s => "TNDA.Campaign.Stage" + s.Number + ".Cleared"))
                .Concat(Enum.GetValues(typeof(VehicleAbility)).Cast<VehicleAbility>().Select(a => "TNDA.Upgrades." + a));
            foreach (string key in keys) { save[key] = (PlayerPrefs.HasKey(key), PlayerPrefs.GetInt(key)); PlayerPrefs.DeleteKey(key); }
            scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/GAME3_Mode7.unity");
            game = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<DemolitionGame>(true)).Single();
            game.PreparePreview();
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

        private void Run(float seconds)
        {
            for (float t = 0; t < seconds; t += Dt) game.Advance(Dt, new ArcadeInput(), new ArcadeInput());
        }

        private void PlayBossStage()
        {
            game.SelectMode(CabinetMode.Campaign);
            game.StartRun(1, stage: Campaign.BossFight);
            while (game.Phase == ArcadePhase.Countdown) Run(Dt);
            Assert.That(game.Phase, Is.EqualTo(ArcadePhase.Playing));
        }

        [Test]
        public void OnlyTheBossStageIsPlayedInTheDerbyArena()
        {
            Assert.That(Campaign.BossFight.Arena, Is.EqualTo(CampaignArena.Derby));
            Assert.That(Campaign.Tutorial.Arena, Is.EqualTo(CampaignArena.TrainingGround));
            Assert.That(Campaign.Pyramid.Arena, Is.EqualTo(CampaignArena.Standard));
            Assert.That(game.Arena, Is.EqualTo(CampaignArena.Standard), "the title demo");
        }

        [Test]
        public void TheDerbyHasOnlyALoaderAndWreckedHulksAndNoSpikeBalls()
        {
            PlayBossStage();
            Assert.That(game.Arena, Is.EqualTo(CampaignArena.Derby));
            var terrain = game.Terrain;
            Assert.That(terrain, Is.SameAs(game.derbyObstacles));
            Assert.That(terrain.Count(o => o.kind == ObstacleKind.Loader), Is.EqualTo(1));
            Assert.That(terrain.Count(o => o.kind == ObstacleKind.Hulk), Is.InRange(2, 3));
            Assert.That(terrain.All(o => o.kind == ObstacleKind.Loader || o.kind == ObstacleKind.Hulk), Is.True);
            Assert.That(game.SpikeBallsInPlay, Is.False);
            Assert.That(game.props.Length, Is.GreaterThan(0), "crates and barrels still turn up");
        }

        [Test]
        public void ANormalMatchAfterTheBossStageKeepsTheUsualTerrain()
        {
            PlayBossStage();
            game.Back();
            game.SelectMode(CabinetMode.Normal);
            game.StartRun(1);
            while (game.Phase == ArcadePhase.Countdown) Run(Dt);
            Assert.That(game.Arena, Is.EqualTo(CampaignArena.Standard));
            Assert.That(game.Terrain, Is.SameAs(game.obstacles));
            Assert.That(game.SpikeBallsInPlay, Is.EqualTo(game.spikeBalls.Length > 0));
        }

        // The derby's terrain keeps clear of every starting spot and crate, and leaves the boss a straight run at the
        // player from its starting spot.
        [Test]
        public void TheDerbyTerrainLeavesTheStartsCratesAndTheBossChargeClear()
        {
            PlayBossStage();
            var terrain = game.Terrain;
            foreach (var car in game.cars.Take(Campaign.BossFight.CarCount))
            {
                Assert.That(terrain.Any(o => o.Contains(car.position, car.Radius + .3f)), Is.False, "start of seat " + car.index);
            }
            foreach (var pickup in game.pickups)
                Assert.That(terrain.Any(o => o.Contains(pickup.point, .65f)), Is.False, "crate at " + pickup.point);
            Vector2 from = game.Boss.position, to = game.cars[0].position;
            foreach (var obstacle in terrain)
                Assert.That(obstacle.Sweep(from, to, game.Boss.Radius, out _, out _, out _), Is.False,
                    obstacle.kind + " at " + obstacle.point + " blocks the boss's charge");
        }

        // The user's 2026-11-22 call: minions drive their own car sheet instead of a checker pattern.
        [Test]
        public void NoComputerCarWearsAPatternAndTheGaragePatternsStay()
        {
            game.Decals.Clear();
            PlayerPrefs.SetInt("TNDA.Gold", 1000);
            Assert.That(game.Decals.TryBuy(CarDecal.Flames, new GoldWallet()), Is.True);
            Assert.That(game.Decals.Equip(0, CarDecal.Flames), Is.True);
            PlayBossStage();
            Assert.That(game.cars.Where(c => c.computer).All(c => game.ShownDecal(c) == CarDecal.None), Is.True);
            Assert.That(game.ShownDecal(game.cars[0]), Is.EqualTo(CarDecal.Flames), "the player's own pattern");
            Assert.That(game.Decals.Equipped(0), Is.EqualTo(CarDecal.Flames));
            Assert.That(PlayerPrefs.GetInt("TNDA.Decals.P1"), Is.EqualTo((int)CarDecal.Flames));
        }

        [Test]
        public void TheBossAndItsMinionsDriveTheirOwnSheets()
        {
            var stage = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Mode7Stage>(true)).Single();
            Assert.That(stage.bossFrames, Has.Length.EqualTo(16), "CarSheetBoss.png wired");
            Assert.That(stage.minionFrames, Has.Length.EqualTo(16), "CarSheetMinion.png wired");
            Assert.That(stage.FramesFor(game.cars[3]), Is.SameAs(stage.carFrames), "no boss outside the stage");
            PlayBossStage();
            var minions = game.cars.Where(c => c.team == 2 && c != game.Boss).ToArray();
            Assert.That(stage.FramesFor(game.Boss), Is.SameAs(stage.bossFrames));
            Assert.That(minions.All(m => stage.FramesFor(m) == stage.minionFrames), Is.True);
            Assert.That(game.cars.Where(c => c.team == 1).All(c => stage.FramesFor(c) == stage.carFrames), Is.True, "player and allies");
            // The bigger boss sheet is drawn at a normal cell's size; only the boss's scale (its footprint) enlarges it.
            Assert.That(stage.bossFrames[0].bounds.size.x, Is.EqualTo(stage.carFrames[0].bounds.size.x).Within(.02f));
            Assert.That(game.Boss.transform.localScale.x, Is.EqualTo(game.Boss.size).Within(1e-4f));
            Assert.That(stage.minionFrames[0].bounds.size.x, Is.EqualTo(stage.carFrames[0].bounds.size.x).Within(.02f));
            var saved = stage.bossFrames;
            stage.bossFrames = new Sprite[0];
            Assert.That(stage.FramesFor(game.Boss), Is.SameAs(stage.carFrames), "without the sheet");
            stage.bossFrames = saved;
        }
    }
}
