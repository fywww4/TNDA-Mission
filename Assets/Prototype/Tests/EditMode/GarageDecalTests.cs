using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TNDA.Prototype.Tests
{
    // Ticket 16: the garage's 外觀 tab, its save, and which pattern each car shows in a match.
    public class GarageDecalTests
    {
        private Scene scene;
        private DemolitionGame game;
        private readonly Dictionary<string, (bool exists, int value)> save = new Dictionary<string, (bool, int)>();

        [SetUp]
        public void OpenCabinetWithACleanIsolatedSave()
        {
            var keys = new[] { "TNDA.Gold", ArcadeGame.ControlsSaveKey, "WreckBest",
                    "TNDA.Decals.P1", "TNDA.Decals.P2" }
                .Concat(Enum.GetValues(typeof(CarDecal)).Cast<CarDecal>().Select(d => "TNDA.Decals.Owned." + d))
                .Concat(Enum.GetValues(typeof(VehicleAbility)).Cast<VehicleAbility>().Select(a => "TNDA.Upgrades." + a))
                .Concat(Challenges.All.SelectMany(c => new[] { "TNDA.Challenge." + c.Id + ".Best", "TNDA.Challenge." + c.Id + ".Unlocked" }));
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

        private void Reopen()
        {
            EditorSceneManager.ClosePreviewScene(scene);
            Open();
        }

        private void Fund(int gold)
        {
            EditorSceneManager.ClosePreviewScene(scene);
            PlayerPrefs.SetInt("TNDA.Gold", gold);
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

        private void PickDecalRow(CarDecal decal)
        {
            while (game.GarageCursor != (int)decal) game.MoveGarageCursor(1);
        }

        [Test]
        public void DecalTabListsNoneAndFourPatternsAndBuyingNeedsEnoughGold()
        {
            Fund(150);
            game.OpenGarage();
            Assert.That(game.GarageTab, Is.EqualTo(GarageTab.Abilities));
            game.SwitchGarageTab();
            Assert.That(game.GarageTab, Is.EqualTo(GarageTab.Decals));
            Assert.That(game.GarageCursor, Is.Zero);
            game.MoveGarageCursor(-1);
            Assert.That(game.GarageCursor, Is.EqualTo(4), "None plus four patterns wrap round");
            Assert.That(game.TryBuySelectedUpgrade(), Is.False, "the ability buy does nothing on the decal tab");
            PickDecalRow(CarDecal.Flames);
            Assert.That(game.UseSelectedDecal(0), Is.False);
            Assert.That(game.GarageMessage, Is.EqualTo(CabinetText.GarageGoldShort));
            Assert.That(game.Gold, Is.EqualTo(150));
            PickDecalRow(CarDecal.TwinStripes);
            Assert.That(game.UseSelectedDecal(0), Is.True);
            Assert.That(game.Gold, Is.EqualTo(50));
            Assert.That(game.Decals.Owns(CarDecal.TwinStripes), Is.True);
            Assert.That(game.Decals.Equipped(0), Is.EqualTo(CarDecal.TwinStripes), "buying puts it on the buyer's car");
            Assert.That(game.Decals.Equipped(1), Is.EqualTo(CarDecal.None));
            game.SwitchGarageTab();
            Assert.That(game.GarageTab, Is.EqualTo(GarageTab.Abilities));
        }

        [Test]
        public void SeatsEquipIndependentlyAndTheChoiceSurvivesReloadButNotALongDelete()
        {
            Fund(1000);
            game.OpenGarage();
            game.SwitchGarageTab();
            PickDecalRow(CarDecal.Checker);
            Assert.That(game.UseSelectedDecal(1), Is.True);
            PickDecalRow(CarDecal.Lightning);
            Assert.That(game.UseSelectedDecal(0), Is.True);
            Assert.That(game.Gold, Is.EqualTo(200));
            PickDecalRow(CarDecal.Checker);
            Assert.That(game.UseSelectedDecal(0), Is.True, "an owned pattern equips for free");
            Assert.That(game.Gold, Is.EqualTo(200));
            PickDecalRow(CarDecal.None);
            Assert.That(game.UseSelectedDecal(1), Is.True);
            Reopen();
            Assert.That(game.Decals.Equipped(0), Is.EqualTo(CarDecal.Checker));
            Assert.That(game.Decals.Equipped(1), Is.EqualTo(CarDecal.None));
            Assert.That(game.Decals.Owns(CarDecal.Lightning), Is.True);
            game.Advance(3.01f, new ArcadeInput { resetSaveHeld = true }, new ArcadeInput());
            Reopen();
            Assert.That(game.Decals.Owns(CarDecal.Lightning), Is.False);
            Assert.That(game.Decals.Equipped(0), Is.EqualTo(CarDecal.None));
        }

        [Test]
        public void OnlyHumanCarsShowTheirPatternAndCherryHidesItUntilItEnds()
        {
            Fund(1000);
            game.OpenGarage();
            game.SwitchGarageTab();
            PickDecalRow(CarDecal.Checker);
            game.UseSelectedDecal(0);
            PickDecalRow(CarDecal.Flames);
            game.UseSelectedDecal(1);
            game.ShowAttract();
            game.StartRun(2);
            game.Advance(4, new ArcadeInput(), new ArcadeInput());
            Assert.That(game.ShownDecal(game.cars[0]), Is.EqualTo(CarDecal.Checker));
            Assert.That(game.ShownDecal(game.cars[1]), Is.EqualTo(CarDecal.Flames));
            for (int i = 2; i < game.cars.Length; i++) Assert.That(game.ShownDecal(game.cars[i]), Is.EqualTo(CarDecal.None));
            game.cars[0].GiveCherry(1);
            Assert.That(game.ShownDecal(game.cars[0]), Is.EqualTo(CarDecal.None));
            game.Advance(1.5f, new ArcadeInput(), new ArcadeInput());
            Assert.That(game.ShownDecal(game.cars[0]), Is.EqualTo(CarDecal.Checker));

            game.PreparePreview();
            game.StartRun(1);
            game.Advance(4, new ArcadeInput(), new ArcadeInput());
            Assert.That(game.ShownDecal(game.cars[1]), Is.EqualTo(CarDecal.None), "the 1P computer partner never wears P2's pattern");
        }
    }
}
