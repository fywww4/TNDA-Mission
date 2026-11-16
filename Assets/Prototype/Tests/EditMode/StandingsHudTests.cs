using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TNDA.Prototype.Tests
{
    // The real cabinet, through its public match/input API and rendered view state.
    public class StandingsHudTests
    {
        private Scene scene;
        private DemolitionGame game;
        private readonly Dictionary<string, (bool exists, int value)> save = new Dictionary<string, (bool, int)>();

        [SetUp]
        public void OpenCabinetWithAnIsolatedSave()
        {
            var keys = new[] { "TNDA.Gold", "WreckBest", ArcadeGame.TransmissionSaveKey }
                .Concat(Enum.GetValues(typeof(VehicleAbility)).Cast<VehicleAbility>().Select(a => "TNDA.Upgrades." + a));
            foreach (var key in keys)
            {
                save[key] = (PlayerPrefs.HasKey(key), PlayerPrefs.GetInt(key));
                PlayerPrefs.DeleteKey(key);
            }
            scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/GAME3_Mode7.unity");
            game = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<DemolitionGame>(true)).Single();
            game.PreparePreview();
        }

        [TearDown]
        public void RestoreUsersSave()
        {
            if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
            foreach (var entry in save)
                if (entry.Value.exists) PlayerPrefs.SetInt(entry.Key, entry.Value.value); else PlayerPrefs.DeleteKey(entry.Key);
            save.Clear();
            PlayerPrefs.Save();
        }

        private void Start(int players)
        {
            game.StartRun(players);
            game.Advance(game.countdownSeconds + .01f, default, default);
            game.Advance(0, default, default);
        }

        [Test]
        public void SelectShowsOnlyItsSeatsTableAndReleaseClosesIt()
        {
            Start(2);
            var p1 = game.standingsHud.views[0];
            var p2 = game.standingsHud.views[1];
            Assert.That(p1.compact.activeInHierarchy && p2.compact.activeInHierarchy, Is.True);
            Assert.That(p1.table.activeInHierarchy || p2.table.activeInHierarchy, Is.False);
            var held = new ArcadeInput { selectHeld = true };

            game.Advance(0, held, default);
            Assert.That(p1.table.activeInHierarchy, Is.True);
            Assert.That(p1.compact.activeInHierarchy, Is.False);
            Assert.That(p2.table.activeInHierarchy, Is.False);
            Assert.That(p2.compact.activeInHierarchy, Is.True);

            game.Advance(0, default, held);
            Assert.That(p1.table.activeInHierarchy, Is.False);
            Assert.That(p2.table.activeInHierarchy, Is.True);
            game.Advance(0, held, held);
            Assert.That(p1.table.activeInHierarchy && p2.table.activeInHierarchy, Is.True);
            game.Advance(0, default, default);
            Assert.That(p1.table.activeInHierarchy || p2.table.activeInHierarchy, Is.False);
        }

        [Test]
        public void TablesAppearOnlyWithinTheConfiguredEliminationWarningAndCanBeDisabled()
        {
            Start(2);
            game.cars[0].score = 99999;
            game.cars[1].score = 99998;
            game.Advance(34.99f, default, default);
            Assert.That(game.standingsHud.views.All(v => !v.table.activeInHierarchy), Is.True);
            game.Advance(.02f, default, default);
            Assert.That(game.standingsHud.views.All(v => v.table.activeInHierarchy), Is.True);
            game.standingsHud.autoShowBeforeElimination = false;
            game.Advance(0, default, default);
            Assert.That(game.standingsHud.views.All(v => !v.table.activeInHierarchy), Is.True);
            game.Advance(0, new ArcadeInput { selectHeld = true }, default);
            Assert.That(game.standingsHud.views[0].table.activeInHierarchy, Is.True, "SELECT still works with the automatic warning off");
            game.standingsHud.autoShowBeforeElimination = true;
            game.standingsHud.autoShowSeconds = 3;
            game.Advance(0, default, default);
            Assert.That(game.standingsHud.views.All(v => !v.table.activeInHierarchy), Is.True);
            game.Advance(2, default, default);
            Assert.That(game.standingsHud.views.All(v => v.table.activeInHierarchy), Is.True);
            game.Advance(3.01f, default, default);
            Assert.That(game.standingsHud.views.All(v => !v.table.activeInHierarchy), Is.True, "The table closes after the elimination");
        }

        [Test]
        public void PlacesFollowScoreDurabilityAndSeatAndUseTheRemainingCarCount()
        {
            Start(2);
            foreach (var car in game.cars) car.score = 0;
            game.cars[0].score = 550;
            game.cars[1].score = 750;
            game.cars[2].score = 550;
            game.cars[0].health = .5f;
            game.Advance(0, default, default);
            var p1 = game.standingsHud.views[0];
            var p2 = game.standingsHud.views[1];
            Assert.That(p1.place.text, Is.EqualTo("3<size=11>/8</size>"));
            Assert.That(p2.place.text, Is.EqualTo("1<size=11>/8</size>"));
            Assert.That(p1.score.text, Is.EqualTo("P1　分數 00550"));
            game.cars[0].health = 1;
            game.Advance(0, default, default);
            Assert.That(p1.place.text, Is.EqualTo("2<size=11>/8</size>"), "The lower seat wins the remaining tie");
            game.cars[7].Eliminate(8, new VehicleLifecycleRules());
            game.cars[6].Eliminate(7, new VehicleLifecycleRules());
            game.cars[5].Eliminate(6, new VehicleLifecycleRules());
            game.cars[4].Eliminate(5, new VehicleLifecycleRules());
            game.Advance(0, new ArcadeInput { selectHeld = true }, default);
            Assert.That(p1.place.text, Is.EqualTo("2<size=11>/4</size>"));
            Assert.That(p1.rows.text, Does.Contain("<color=#A0A0A0>8 P8 00000 耐久100　淘汰</color>"));
            foreach (var car in game.cars) Assert.That(p1.rows.text, Does.Contain(car.DriverName));
        }

        [Test]
        public void ViewsStayInTheirOwnHalfAndHideOutsideTheRace()
        {
            Assert.That(game.standingsHud.views.All(v => !v.root.gameObject.activeInHierarchy), Is.True);
            game.OpenGarage();
            game.Advance(0, default, default);
            Assert.That(game.standingsHud.views.All(v => !v.root.gameObject.activeInHierarchy), Is.True);
            Start(2);
            Assert.That(game.standingsHud.views[0].root.anchoredPosition, Is.EqualTo(new Vector2(0, 224)));
            Assert.That(game.standingsHud.views[1].root.anchoredPosition, Is.EqualTo(Vector2.zero));
            foreach (var view in game.standingsHud.views) Assert.That(view.root.sizeDelta, Is.EqualTo(new Vector2(512, 224)));
            Start(1);
            Assert.That(game.standingsHud.views[0].root.sizeDelta, Is.EqualTo(new Vector2(512, 448)));
            Assert.That(game.standingsHud.views[1].root.gameObject.activeInHierarchy, Is.False);
            game.Back();
            game.Advance(0, default, default);
            Assert.That(game.standingsHud.views.All(v => !v.root.gameObject.activeInHierarchy), Is.True);
            string characters = string.Concat(CabinetText.All);
            Assert.That(characters.Where(c => !char.IsWhiteSpace(c)).All(c => game.header.font.HasCharacter(c)), Is.True);
        }
    }
}
