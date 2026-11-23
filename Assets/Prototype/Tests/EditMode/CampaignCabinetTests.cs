using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TNDA.Prototype.Tests
{
    // The campaign on the title and through a stage (ticket 15), on the real Mode 7 scene: the third mode tab, locked
    // stages, the story pages, the training ground, and the result panel's next stage / retry.
    public class CampaignCabinetTests
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
            scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/GAME3_Mode7.unity");
            game = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<DemolitionGame>(true)).Single();
            game.PreparePreview();
            game.SelectMode(CabinetMode.Campaign);
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

        private void PlayThrough()
        {
            while (game.Phase == ArcadePhase.Countdown) Run(Dt);
            Assert.That(game.Phase, Is.EqualTo(ArcadePhase.Playing));
        }

        [Test]
        public void LeftAndRightGoRoundTheThreeModes()
        {
            game.SelectMode(CabinetMode.Normal);
            game.MoveMode(1);
            Assert.That(game.Mode, Is.EqualTo(CabinetMode.Challenge));
            game.MoveMode(1);
            Assert.That(game.Mode, Is.EqualTo(CabinetMode.Campaign));
            game.MoveMode(1);
            Assert.That(game.Mode, Is.EqualTo(CabinetMode.Normal));
            game.MoveMode(-1);
            Assert.That(game.Mode, Is.EqualTo(CabinetMode.Campaign));
        }

        [Test]
        public void ALockedStageCannotBeStarted()
        {
            game.MoveStageCursor(1);
            Assert.That(game.SelectedStage, Is.SameAs(Campaign.Pyramid));
            game.PressStart();
            Assert.That(game.Phase, Is.EqualTo(ArcadePhase.Attract));
            Assert.That(game.ActiveStage, Is.Null);
        }

        [Test]
        public void TheFirstStageTellsItsStoryThenPlaysOnTheTrainingGround()
        {
            game.PressStart();
            Assert.That(game.Phase, Is.EqualTo(ArcadePhase.Story));
            Assert.That(game.ActiveStage, Is.SameAs(Campaign.Tutorial));
            Assert.That(game.StoryPage, Is.Zero);
            Assert.That(game.overlay.activeSelf, Is.True);
            Assert.That(game.overlayBody.text, Does.Contain("歡迎"));
            Assert.That(game.TrainingGround, Is.True, "the field behind the story is already the stage's");
            for (int page = 1; page < Campaign.Tutorial.Story.Count; page++)
            {
                game.PressStart();
                Assert.That(game.StoryPage, Is.EqualTo(page));
            }
            game.PressStart();
            Assert.That(game.Phase, Is.EqualTo(ArcadePhase.Countdown).Or.EqualTo(ArcadePhase.Playing));
            PlayThrough();
            Assert.That(game.cars.Count(c => c.gameObject.activeSelf), Is.EqualTo(4));
            Assert.That(game.SpikeBallsInPlay, Is.False);
            Assert.That(game.Coach, Is.Not.Null);
            Assert.That(game.arenaBoard.text, Does.Contain(CabinetText.TutorialSteps[0]));
        }

        [Test]
        public void EscDuringTheStoryGoesBackToTheCampaignList()
        {
            game.PressStart();
            game.Back();
            Assert.That(game.Phase, Is.EqualTo(ArcadePhase.Attract));
            Assert.That(game.Mode, Is.EqualTo(CabinetMode.Campaign));
            Assert.That(game.TrainingGround, Is.False);
        }

        [Test]
        public void ClearingAStageSavesItAndStartGoesOnToTheNextStory()
        {
            game.Progress.MarkCleared(Campaign.Tutorial);
            game.Progress.MarkCleared(Campaign.Pyramid);
            game.StartRun(1, stage: Campaign.Pyramid);
            PlayThrough();
            // The player outscores everyone to the end of the pyramid.
            for (float t = 0; t < 125 && game.Phase == ArcadePhase.Playing; t += Dt)
            {
                game.cars[0].score = 99999;
                game.cars[0].spawnProtection = 1;
                Run(Dt);
            }
            Assert.That(game.Phase, Is.EqualTo(ArcadePhase.Results));
            Assert.That(game.StageCleared, Is.True);
            Assert.That(game.overlayTitle.text, Is.EqualTo(string.Format(CabinetText.StageClear, 2)));
            game.PressStart();
            Assert.That(game.Phase, Is.EqualTo(ArcadePhase.Story));
            Assert.That(game.ActiveStage, Is.SameAs(Campaign.BossFight));
        }

        [Test]
        public void WreckingTheBossFinishesTheCampaignAndAFailedStageRetriesAtOnce()
        {
            game.StartRun(1, stage: Campaign.BossFight);
            PlayThrough();
            Assert.That(game.cars.Count(c => c.gameObject.activeSelf), Is.EqualTo(6));
            Assert.That(game.Boss, Is.Not.Null);
            Assert.That(game.Boss.transform.localScale.x, Is.EqualTo(game.Boss.size).Within(1e-4f), "drawn bigger");
            Assert.That(game.arenaBoard.text, Does.StartWith(CabinetText.BossName));
            Run(2.5f);
            game.Boss.spawnProtection = 0;
            game.Boss.shieldSeconds = 0;
            game.DamageVehicle(game.Boss, 99);
            Assert.That(game.Phase, Is.EqualTo(ArcadePhase.Results));
            Assert.That(game.StageCleared, Is.True);
            Assert.That(game.Progress.IsCleared(Campaign.BossFight), Is.True);
            Assert.That(game.overlayTitle.text, Is.EqualTo(CabinetText.CampaignComplete));

            game.StartRun(1, stage: Campaign.BossFight);
            PlayThrough();
            Run(2.5f);
            game.cars[0].spawnProtection = 0;
            game.cars[0].shieldSeconds = 0;
            game.DamageVehicle(game.cars[0], 99);
            Assert.That(game.Phase, Is.EqualTo(ArcadePhase.Results));
            Assert.That(game.StageCleared, Is.False);
            game.PressStart();
            Assert.That(game.Phase, Is.EqualTo(ArcadePhase.Countdown).Or.EqualTo(ArcadePhase.Playing), "a retry skips the story");
            Assert.That(game.ActiveStage, Is.SameAs(Campaign.BossFight));
        }

        [Test]
        public void ANormalMatchAfterTheCampaignHasNoTeamsOrBoss()
        {
            game.StartRun(1, stage: Campaign.BossFight);
            PlayThrough();
            game.Back();
            game.SelectMode(CabinetMode.Normal);
            game.StartRun(1);
            PlayThrough();
            Assert.That(game.Boss, Is.Null);
            Assert.That(game.cars.All(c => c.team == 0 && c.size == 1 && c.armor == 1 && string.IsNullOrEmpty(c.nameTag)), Is.True);
            Assert.That(game.cars.All(c => c.transform.localScale == Vector3.one), Is.True);
        }
    }
}
