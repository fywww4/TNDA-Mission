using System;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TNDA.Prototype.Tests
{
    public class UpgradeDrivingTests
    {
        private Scene scene;
        private ArcadeVehicle car;
        private DemolitionMatch match;
        private VehicleUpgrades upgrades;
        private GoldWallet wallet;
        private string prefix;
        private static readonly ArcadeInput Gas = new ArcadeInput { throttle = 1 };

        [SetUp]
        public void CreateMatch()
        {
            prefix = "TNDA.UpgradeDrive.Tests." + Guid.NewGuid().ToString("N") + ".";
            upgrades = new VehicleUpgrades(prefix);
            wallet = new GoldWallet(prefix + "Gold");
            wallet.AwardRound((1, 1000000));
            scene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Upgrade car");
            SceneManager.MoveGameObjectToScene(root, scene);
            car = root.AddComponent<ArcadeVehicle>();
            match = new DemolitionMatch(new[] { car }, Array.Empty<BreakableProp>(), Array.Empty<ArenaPickup>(),
                Array.Empty<ArcadeProjectile>(), new MatchTuning {
                    playerUpgrades = upgrades, arenaMin = Vector2.one * -1000, arenaMax = Vector2.one * 1000
                });
            match.Reset(1, true);
        }

        [TearDown]
        public void Cleanup()
        {
            EditorSceneManager.ClosePreviewScene(scene);
            upgrades.Clear();
            wallet.Clear();
        }

        [TestCase(0, 2.8f, -1f, 1.2f, 1.9f, 2.5f, 3f)]
        [TestCase(1, 2.94f, -1.05f, 1.26f, 1.995f, 2.625f, 3.15f)]
        [TestCase(2, 3.08f, -1.1f, 1.32f, 2.09f, 2.75f, 3.3f)]
        [TestCase(3, 3.22f, -1.15f, 1.38f, 2.185f, 2.875f, 3.45f)]
        [TestCase(4, 3.36f, -1.2f, 1.44f, 2.28f, 3f, 3.6f)]
        public void SpeedUpgradeScalesAutomaticAndEveryManualGear(int level, float auto, float reverse, float first, float second, float third, float fourth)
        {
            for (int i = 0; i < level; i++) upgrades.TryBuy(VehicleAbility.TopSpeed, wallet);
            match.Tick(10, Gas, Gas);
            Assert.That(car.speed, Is.EqualTo(auto).Within(.0001f), "automatic");
            float[] limits = { reverse, first, second, third, fourth };
            match.Tuning.manualTransmission = true;
            for (int gear = 0; gear <= 4; gear++)
            {
                match.Reset(1, true);
                car.position = Vector2.zero;
                while (car.Gear != gear)
                    match.Tick(.001f, new ArcadeInput { shiftUpPressed = car.Gear < gear, shiftDownPressed = car.Gear > gear }, Gas);
                match.Tick(10, Gas, Gas);
                Assert.That(car.speed, Is.EqualTo(limits[gear]).Within(.0001f), "gear " + gear);
            }
        }

        [TestCase(0, .35f, -34.875f, .62f)]
        [TestCase(1, .3675f, -36.61875f, .639f)]
        [TestCase(2, .385f, -38.3625f, .658f)]
        [TestCase(3, .4025f, -40.10625f, .677f)]
        [TestCase(4, .42f, -41.85f, .696f)]
        public void AccelerationHandlingAndReceivedDamageHaveFivePercentSteps(int level, float speed, float turn, float health)
        {
            for (int i = 0; i < level; i++)
                foreach (VehicleAbility ability in Enum.GetValues(typeof(VehicleAbility))) upgrades.TryBuy(ability, wallet);
            car.position = Vector2.zero;
            car.heading = 0;
            match.Tick(.25f, new ArcadeInput { throttle = 1, steer = 1 }, Gas);
            Assert.That(car.speed, Is.EqualTo(speed).Within(.0001f));
            Assert.That(car.heading, Is.EqualTo(turn).Within(.0001f));
            car.spawnProtection = 0;
            match.ApplyDamage(car, .38f);
            Assert.That(car.health, Is.EqualTo(health).Within(.0001f));
            car.speed = 1;
            match.Tick(.25f, new ArcadeInput(), Gas);
            Assert.That(car.speed, Is.EqualTo(.45f).Within(.0001f), "upgrade leaves coasting unchanged");
        }
    }
}
