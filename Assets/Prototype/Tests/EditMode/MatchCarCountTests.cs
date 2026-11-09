using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TNDA.Prototype.Tests
{
    // The match works out its rules from the number of cars and the elimination schedule. The four-car arcade match
    // must play exactly as it did when four cars were written into the rules, and other car counts must run to a champion.
    public class MatchCarCountTests
    {
        private const float Dt = 1f / 60;
        private Scene scene;

        [SetUp]
        public void OpenScene() => scene = EditorSceneManager.NewPreviewScene();

        [TearDown]
        public void CloseScene() => EditorSceneManager.ClosePreviewScene(scene);

        private DemolitionMatch CreateMatch(int carCount, MatchTuning tuning = null)
        {
            var cars = new ArcadeVehicle[carCount];
            for (int i = 0; i < cars.Length; i++)
            {
                var root = new GameObject("Car " + i);
                SceneManager.MoveGameObjectToScene(root, scene);
                cars[i] = root.AddComponent<ArcadeVehicle>();
                cars[i].index = i;
            }
            var props = new[] {
                new BreakableProp { point = new Vector2(-1.5f, -2.5f) }, new BreakableProp { point = new Vector2(1.5f, -2.5f), points = 75 },
                new BreakableProp { point = new Vector2(0, -.5f) }, new BreakableProp { point = new Vector2(-2.8f, 0), points = 150 },
                new BreakableProp { point = new Vector2(2.8f, 0) }
            };
            var pickups = new[] {
                new ArenaPickup { point = new Vector2(0, 1) }, new ArenaPickup { point = new Vector2(-2.5f, -1.5f) },
                new ArenaPickup { point = new Vector2(2.5f, -1.5f) }, new ArenaPickup { point = new Vector2(0, -3) }
            };
            var rockets = new[] { new ArcadeProjectile(), new ArcadeProjectile() };
            return new DemolitionMatch(cars, props, pickups, rockets, tuning ?? new MatchTuning());
        }

        // A player who holds the gas, weaves, and fires whatever they hold every few seconds.
        private static ArcadeInput ScriptedPlayer(int frame, int seat) => new ArcadeInput {
            throttle = 1, steer = Mathf.Sin(frame * .021f + seat * 1.7f),
            action = frame % 240 == 120, actionPressed = frame % 240 == 120
        };

        // Plays a seeded match for up to this long (or to its end), handing over each frame's events before clearing them.
        private static void Play(DemolitionMatch match, int humans, bool demo, int seed, float seconds, System.Action<IReadOnlyList<MatchEvent>> onEvents)
        {
            Random.InitState(seed);
            match.Reset(humans, demo);
            for (int frame = 0; frame < seconds * 60 && !match.Finished; frame++)
            {
                match.Tick(Dt, ScriptedPlayer(frame, 0), ScriptedPlayer(frame, 1));
                match.AdvanceSpinOuts(Dt, Vector2.zero, 6);
                onEvents(match.Events);
                match.ClearEvents();
            }
        }

        // Plays a seeded match and condenses everything that happened into one line per car plus the outcome.
        private static string Fingerprint(DemolitionMatch match, int humans, bool demo, int seed, float seconds)
        {
            int events = 0;
            Play(match, humans, demo, seed, seconds, reported => events += reported.Count);
            var text = new StringBuilder();
            foreach (var car in match.Cars)
                text.AppendFormat("{0}:{1:0} {2:0.000} ({3:0.000},{4:0.000}) {5} {6}|", car.index, car.score, car.health,
                    car.position.x, car.position.y, car.eliminated ? "out" : car.alive ? "in" : "down", car.FinalPlace);
            text.AppendFormat("champion {0} wrecks {1} events {2} t {3:0.00}", match.Champion, match.Wrecks, events, match.Elapsed);
            return text.ToString();
        }

        // Recorded from the rules before car count and the elimination schedule were made data.
        [TestCase(1, false, 11,
            "0:0 0.667 (-6.086,1.350) out 4|1:3263 0.872 (-2.532,0.932) out 2|2:4340 1.000 (2.228,-2.558) in 1|3:1693 0.936 (6.082,-4.951) out 3|champion 2 wrecks 19 events 324 t 120.01")]
        [TestCase(2, false, 22,
            "0:0 0.414 (-6.070,-1.690) out 4|1:100 0.320 (3.824,6.050) out 3|2:3072 0.186 (-3.774,-0.977) out 2|3:7086 0.731 (-4.081,-2.360) in 1|champion 3 wrecks 8 events 335 t 120.01")]
        [TestCase(0, true, 33,
            "0:1774 1.000 (-2.969,-0.377) in 0|1:1967 0.540 (-3.421,-0.808) in 0|2:1005 0.669 (-2.666,-1.594) in 0|3:1853 1.000 (-0.610,-0.670) in 0|champion -1 wrecks 12 events 234 t 60.00")]
        public void TheFourCarMatchPlaysAsBefore(int humans, bool demo, int seed, string expected)
        {
            var match = CreateMatch(4);
            string actual = Fingerprint(match, humans, demo, seed, demo ? 60 : 125);
            Assert.AreEqual(expected, actual, "Fingerprint: " + actual);
        }

        // Plays to the end, with every car run by the AI except the given players, and returns the knock-out places in order.
        private static int[] PlayToTheEnd(DemolitionMatch match, int humans)
        {
            var places = new List<int>();
            Play(match, humans, false, 7, 130, reported => places.AddRange(reported.OfType<VehicleEliminated>().Select(e => e.Place)));
            return places.ToArray();
        }

        [Test]
        public void AnEightCarMatchRunsToAChampion()
        {
            var match = CreateMatch(8);

            CollectionAssert.AreEqual(new[] { 8, 7, 6, 5, 4, 3, 2 }, PlayToTheEnd(match, 1));
            Assert.IsTrue(match.Finished);
            Assert.That(match.Elapsed, Is.EqualTo(120).Within(.05f));
            var champion = match.Cars[match.Champion];
            Assert.IsFalse(champion.eliminated);
            Assert.AreEqual(1, champion.FinalPlace);
            CollectionAssert.AreEquivalent(Enumerable.Range(1, 8), match.Cars.Select(c => c.FinalPlace));
        }

        [Test]
        public void ThePyramidScheduleKnocksOutFourThenTwoThenOne()
        {
            var tuning = new MatchTuning { eliminations = new[] {
                new EliminationStep(40, 4), new EliminationStep(80, 2), new EliminationStep(120, 1) } };
            var match = CreateMatch(8, tuning);
            match.Reset(2);
            for (int i = 0; i < match.Cars.Length; i++) { match.Cars[i].computer = false; match.Cars[i].score = (i + 1) * 100; }
            var idle = new ArcadeInput();

            while (match.Elapsed < 39.9f) match.Tick(Dt, idle, idle);
            Assert.IsEmpty(match.Events.OfType<VehicleEliminated>());
            while (match.Elapsed < 40.1f) match.Tick(Dt, idle, idle);
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3 }, match.Events.OfType<VehicleEliminated>().Select(e => e.Car.index));
            Assert.AreEqual(4, match.Contenders);
            Assert.AreEqual(80, match.NextElimination);

            while (match.Elapsed < 80.1f) match.Tick(Dt, idle, idle);
            Assert.AreEqual(2, match.Contenders);
            Assert.IsFalse(match.Finished);

            while (!match.Finished && match.Elapsed < 121) match.Tick(Dt, idle, idle);
            Assert.IsTrue(match.Finished);
            Assert.AreEqual(7, match.Champion);
            CollectionAssert.AreEqual(new[] { 8, 7, 6, 5, 4, 3, 2 }, match.Events.OfType<VehicleEliminated>().Select(e => e.Place));
        }

        // The title demo knocks out one car early by hand; the countdown then moves on to the next step, as before.
        [Test]
        public void ADemoKnockoutRunsTheNextStepEarly()
        {
            var match = CreateMatch(4);
            match.Reset(0, true);
            match.EliminateNextStep();

            Assert.AreEqual(1, match.CarsKnockedOut);
            Assert.AreEqual(3, match.Contenders);
            Assert.AreEqual(80, match.NextElimination);
        }

        [Test]
        public void EightCarsStartApart()
        {
            var match = CreateMatch(8);
            match.Reset(1);
            var starts = match.Cars.Select(c => c.position).ToArray();
            for (int i = 0; i < starts.Length; i++) for (int j = i + 1; j < starts.Length; j++)
                Assert.That(Vector2.Distance(starts[i], starts[j]), Is.GreaterThan(1), "start " + i + " and " + j);
        }
    }
}
