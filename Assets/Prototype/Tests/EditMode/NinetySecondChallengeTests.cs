using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TNDA.Prototype.Tests
{
    // The 90-second challenge through the public challenge and match, with idle cars so only the clock decides places.
    public class NinetySecondChallengeTests
    {
        private static readonly ArcadeInput Idle = new ArcadeInput();
        private Scene scene;
        private DemolitionMatch match;

        [SetUp]
        public void CreateMatch()
        {
            scene = EditorSceneManager.NewPreviewScene();
            var cars = new ArcadeVehicle[Challenges.NinetySecondScore.CarCount];
            for (int i = 0; i < cars.Length; i++)
            {
                var root = new GameObject("Car " + i);
                SceneManager.MoveGameObjectToScene(root, scene);
                cars[i] = root.AddComponent<ArcadeVehicle>();
                cars[i].index = i;
            }
            var tuning = new MatchTuning { randomPropSpots = false };
            Challenges.NinetySecondScore.ApplyRules(tuning);
            match = new DemolitionMatch(cars, new BreakableProp[0], new ArenaPickup[0], new ArcadeProjectile[0], tuning);
            match.Reset(1);
            foreach (var car in cars) car.computer = false;
        }

        [TearDown]
        public void CloseScene() => EditorSceneManager.ClosePreviewScene(scene);

        [Test]
        public void EightCarsStayInUntil90SecondsThenFinishInScoreOrder()
        {
            Assert.AreEqual(8, match.Cars.Length);
            float[] scores = { 500, 100, 700, 300, 800, 200, 600, 400 };
            for (int i = 0; i < scores.Length; i++) match.Cars[i].score = scores[i];

            // Quarter-second ticks reach 90 exactly, without accumulated floating-point drift.
            for (int tick = 0; tick < 359; tick++) match.Tick(.25f, Idle, Idle);
            Assert.AreEqual(89.75f, match.Elapsed);
            Assert.AreEqual(8, match.Contenders);
            Assert.IsFalse(match.Finished);
            Assert.IsEmpty(match.Events.OfType<VehicleEliminated>());

            match.Tick(.25f, Idle, Idle);

            Assert.AreEqual(90, match.Elapsed);
            Assert.IsTrue(match.Finished);
            Assert.AreEqual(4, match.Champion);
            CollectionAssert.AreEqual(new[] { 4, 8, 2, 6, 1, 7, 3, 5 }, match.Cars.Select(c => c.FinalPlace));
            CollectionAssert.AreEqual(scores, match.Cars.Select(c => c.score));
            Assert.AreEqual(1, match.Events.OfType<MatchEnded>().Count());
            Assert.AreEqual(500, Challenges.NinetySecondScore.ScoreOf(match, match.Cars[0]), "record the player's score even when another car wins");
        }

        [Test]
        public void WreckingThePlayerPenalizesAndRespawnsWithoutEndingTheRun()
        {
            var player = match.Cars[0];
            player.score = 1000;
            player.spawnProtection = 0;

            match.ApplyDamage(player, 2);

            Assert.IsFalse(player.alive);
            Assert.AreEqual(750, player.score);
            Assert.IsFalse(match.Finished);
            for (int tick = 0; tick < 13; tick++) match.Tick(.25f, Idle, Idle);
            Assert.IsTrue(player.alive);
            Assert.AreEqual(1, player.health);
            Assert.AreEqual(8, match.Contenders);
            Assert.IsEmpty(match.Events.OfType<VehicleEliminated>());
        }
    }
}
