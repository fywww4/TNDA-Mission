using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TNDA.Prototype.Tests
{
    // Exercise the public match with the production scene's cars, pool and elimination schedule.
    public class Mode7PyramidTests
    {
        private const float Dt = 1f / 60;
        private Scene scene;
        private DemolitionGame game;
        private DemolitionMatch match;
        private static readonly ArcadeInput Idle = new ArcadeInput();

        [SetUp]
        public void LoadArcadeMatch()
        {
            scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/GAME3_Mode7.unity");
            game = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<DemolitionGame>(true)).Single();
            match = new DemolitionMatch(game.cars, game.props, game.pickups, game.rockets, new MatchTuning {
                eliminations = game.eliminationSchedule, arenaMin = game.arenaMin, arenaMax = game.arenaMax,
                randomPropSpots = false
            });
        }

        [TearDown]
        public void CloseScene() => EditorSceneManager.ClosePreviewScene(scene);

        private void WaitUntil(float seconds)
        {
            while (!match.Finished && match.Elapsed < seconds) match.Tick(Dt, Idle, Idle);
        }

        [Test]
        public void ArcadeEliminatesHalfAt40And80SecondsThenDecidesTheChampionAt120()
        {
            Assert.AreEqual(8, match.Cars.Length);
            match.Reset(2);
            foreach (var car in match.Cars) { car.computer = false; car.score = (car.index + 1) * 100; }

            WaitUntil(39.9f);
            Assert.AreEqual(8, match.Contenders);
            WaitUntil(40.1f);
            Assert.AreEqual(4, match.Contenders);
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3 }, match.Events.OfType<VehicleEliminated>().Select(e => e.Car.index));
            WaitUntil(79.9f);
            Assert.AreEqual(4, match.Contenders);
            WaitUntil(80.1f);
            Assert.AreEqual(2, match.Contenders);
            Assert.IsFalse(match.Finished);
            WaitUntil(119.9f);
            Assert.IsFalse(match.Finished);
            WaitUntil(120.1f);
            Assert.IsTrue(match.Finished);
            Assert.AreEqual(7, match.Champion);
            CollectionAssert.AreEqual(new[] { 8, 7, 6, 5, 4, 3, 2, 1 }, match.Cars.Select(c => c.FinalPlace));
            CollectionAssert.AreEqual(new[] { 100f, 200, 300, 400, 500, 600, 700, 800 }, match.Cars.Select(c => c.score));
        }

        [TestCase(1, 2, 2, 3)]
        [TestCase(2, 2, 2, 2)]
        [TestCase(0, 2, 2, 4)]
        public void EachModeHasTheRequiredPersonalityMix(int humans, int wanderers, int hunters, int bumpers)
        {
            match.Reset(humans, humans == 0);
            var ai = match.Cars.Where(c => c.computer).ToArray();
            Assert.AreEqual(wanderers, ai.Count(c => c.Ai.Personality == AiPersonality.Wanderer));
            Assert.AreEqual(hunters, ai.Count(c => c.Ai.Personality == AiPersonality.Hunter));
            Assert.AreEqual(bumpers, ai.Count(c => c.Ai.Personality == AiPersonality.Bumper));
            CollectionAssert.AreEqual(new[] { "P1", "P2", "P3", "P4", "P5", "P6", "P7", "P8" }, match.Cars.Select(c => c.DriverName));
        }

        [Test]
        public void PyramidTiesLoseByDurabilityThenHigherSeatWhileScoreTakesPriority()
        {
            match.Reset(2);
            foreach (var car in match.Cars) { car.computer = false; car.score = 100; }
            match.Cars[4].score = 99;
            match.Cars[2].health = .25f;
            match.Cars[0].health = .5f;
            WaitUntil(120.1f);

            CollectionAssert.AreEqual(new[] { 4, 2, 0, 7, 6, 5, 3 }, match.Events.OfType<VehicleEliminated>().Select(e => e.Car.index));
            Assert.AreEqual(1, match.Champion);
        }

        [Test]
        public void AllEightStartAndRespawnApartInsideTheArena()
        {
            match.Reset(2);
            AssertSeparated();
            foreach (var car in match.Cars)
            {
                car.computer = false;
                car.spawnProtection = 0;
                match.ApplyDamage(car, 2);
            }
            WaitUntil(3.1f);
            Assert.That(match.Cars.All(c => c.alive && c.health == 1 && c.spawnProtection > 0), Is.True);
            AssertSeparated();
        }

        private void AssertSeparated()
        {
            foreach (var car in match.Cars)
            {
                Assert.That(car.position.x, Is.InRange(game.arenaMin.x, game.arenaMax.x));
                Assert.That(car.position.y, Is.InRange(game.arenaMin.y, game.arenaMax.y));
                foreach (var other in match.Cars.Where(c => c.index > car.index))
                    Assert.That(Vector2.Distance(car.position, other.position), Is.GreaterThan(1), car.DriverName + " / " + other.DriverName);
            }
        }

        [Test]
        public void FourSharedProjectileSlotsHoldMixedRocketsAndOilWithoutConsumingTheFifthItem()
        {
            match.Reset(1);
            Assert.AreEqual(5, match.Props.Length);
            Assert.AreEqual(4, match.Pickups.Length);
            Assert.AreEqual(4, match.Rockets.Length);
            for (int i = 0; i < 4; i++)
            {
                match.Cars[i].item = i % 2 == 0 ? ArenaItem.Rocket : ArenaItem.Oil;
                Assert.IsTrue(ArenaItems.Use(match.Cars[i], match));
            }
            Assert.AreEqual(2, match.Rockets.Count(r => r.active && !r.slick));
            Assert.AreEqual(2, match.Rockets.Count(r => r.active && r.slick));
            match.Cars[4].item = ArenaItem.Rocket;
            Assert.IsFalse(ArenaItems.Use(match.Cars[4], match));
            Assert.AreEqual(ArenaItem.Rocket, match.Cars[4].item);
        }
    }
}
