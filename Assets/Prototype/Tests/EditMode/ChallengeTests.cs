using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TNDA.Prototype.Tests
{
    // Challenge mode: the rocket-only challenge's rules on the public match, the catalog shown on the title, and the
    // saved best records. Bare cars in a preview scene, like DemolitionMatchTests.
    public class ChallengeTests
    {
        private const float Dt = 1f / 60;
        private static readonly ArcadeInput Idle = new ArcadeInput();
        private Scene scene;
        private ArcadeVehicle[] cars;
        private ArcadeProjectile[] rockets;
        private ArenaPickup[] pickups;
        private DemolitionMatch match;
        private string recordPrefix;

        [SetUp]
        public void CreateMatch()
        {
            recordPrefix = "TNDA.Challenge.Tests." + Guid.NewGuid().ToString("N") + ".";
            scene = EditorSceneManager.NewPreviewScene();
            cars = new ArcadeVehicle[4];
            for (int i = 0; i < cars.Length; i++)
            {
                var root = new GameObject("Car " + i);
                SceneManager.MoveGameObjectToScene(root, scene);
                cars[i] = root.AddComponent<ArcadeVehicle>();
                cars[i].index = i;
            }
            var props = new[] {
                new BreakableProp { point = new Vector2(-1.5f, -2.5f) },
                new BreakableProp { point = new Vector2(1.5f, -2.5f), points = 75 }
            };
            pickups = new[] { new ArenaPickup { point = new Vector2(0, 1) } }; // item None: a random crate
            rockets = new[] { new ArcadeProjectile(), new ArcadeProjectile() };
            var tuning = new MatchTuning { randomPropSpots = false };
            Challenges.RocketOnly.ApplyRules(tuning);
            match = new DemolitionMatch(cars, props, pickups, rockets, tuning);
        }

        [TearDown]
        public void CleanUp()
        {
            EditorSceneManager.ClosePreviewScene(scene);
            new ChallengeRecords(recordPrefix).Clear();
        }

        private void ResetWithHumansOnly()
        {
            match.Reset(1);
            foreach (var car in cars) { car.computer = false; car.spawnProtection = 0; }
        }

        [Test]
        public void CratesOnlyGiveRocketsEvenToTheLastPlaceCarThatWouldRollCherries()
        {
            ResetWithHumansOnly(); // every score is 0, so each car is "trailing"
            for (int i = 0; i < 60; i++)
            {
                cars[0].item = ArenaItem.None;
                cars[0].position = pickups[0].point;
                pickups[0].available = true;
                match.Tick(Dt, Idle, Idle);
                Assert.AreEqual(ArenaItem.Rocket, cars[0].item, "crate " + i);
            }
        }

        [Test]
        public void RamsScoreNothingAndWreckingByRamGivesNoBonus()
        {
            ResetWithHumansOnly();
            cars[0].position = new Vector2(0, -1); cars[0].heading = 0; cars[0].speed = 2.8f;
            cars[3].position = new Vector2(.55f, -1); cars[3].heading = 90; cars[3].health = .01f; cars[3].score = 400;
            match.Tick(Dt, Idle, Idle);

            Assert.IsFalse(cars[3].alive, "the ram still wrecks");
            Assert.AreEqual(0, cars[0].score);
            Assert.IsEmpty(match.Events.OfType<WreckBonus>());
            Assert.IsEmpty(match.Events.OfType<ScoreChanged>().Where(e => e.Car == cars[0]));
        }

        [Test]
        public void RocketHitsAndSmashedPropsStillScore()
        {
            ResetWithHumansOnly();
            cars[3].position = new Vector2(1, -1);
            rockets[0].Spawn(new Vector2(-.5f, -1), new Vector2(6, 0), 0);
            for (int f = 0; f < 20 && rockets[0].active; f++) match.Tick(Dt, Idle, Idle);
            Assert.AreEqual(120, cars[0].score);

            cars[1].position = new Vector2(-2.1f, -2.5f); cars[1].heading = 0; cars[1].speed = 2.8f;
            match.Tick(Dt, Idle, Idle);
            Assert.AreEqual(100, cars[1].score);
        }

        [Test]
        public void NobodyIsEliminatedBeforeTheRoundEndsAt120Seconds()
        {
            ResetWithHumansOnly();
            for (int i = 0; i < cars.Length; i++) cars[i].score = 400 - i * 100;
            while (match.Elapsed < 119.9f) match.Tick(Dt, Idle, Idle);
            Assert.AreEqual(4, match.Contenders);
            Assert.IsFalse(match.Finished);

            while (!match.Finished && match.Elapsed < 121) match.Tick(Dt, Idle, Idle);
            Assert.IsTrue(match.Finished);
            Assert.AreEqual(0, match.Champion);
        }

        [Test]
        public void TheDefaultMatchIsUnchangedByTheChallengeRuleSwitches()
        {
            var normal = new MatchTuning();
            Assert.IsFalse(normal.rocketCratesOnly);
            Assert.IsTrue(normal.ramsScore);
            Assert.AreEqual(3, normal.eliminations.Length);
        }

        [Test]
        public void TheCatalogListsRocketOnlyFreeAndTheOthersLockedWithAPrice()
        {
            var records = new ChallengeRecords(recordPrefix);
            Assert.AreSame(Challenges.RocketOnly, Challenges.All[0]);
            Assert.IsTrue(records.IsUnlocked(Challenges.RocketOnly));
            Assert.AreEqual(0, Challenges.RocketOnly.UnlockPrice);
            Assert.That(Challenges.All.Count, Is.EqualTo(3));
            foreach (var locked in Challenges.All.Skip(1))
            {
                Assert.IsFalse(records.IsUnlocked(locked), locked.Name);
                Assert.AreEqual(150, locked.UnlockPrice, locked.Name);
            }
            Assert.AreEqual(Challenges.All.Count, Challenges.All.Select(c => c.Id).Distinct().Count());
        }

        [Test]
        public void ABestRecordOnlyRisesAndSurvivesReopening()
        {
            var records = new ChallengeRecords(recordPrefix);
            Assert.AreEqual(0, records.Best(Challenges.RocketOnly));
            Assert.IsTrue(records.Submit(Challenges.RocketOnly, 800));
            Assert.IsFalse(records.Submit(Challenges.RocketOnly, 500));
            Assert.IsFalse(records.Submit(Challenges.RocketOnly, 800));
            Assert.AreEqual(800, records.Best(Challenges.RocketOnly));
            Assert.AreEqual(800, new ChallengeRecords(recordPrefix).Best(Challenges.RocketOnly));
            Assert.IsTrue(records.Submit(Challenges.RocketOnly, 900));
            Assert.AreEqual(900, new ChallengeRecords(recordPrefix).Best(Challenges.RocketOnly));
        }

        [Test]
        public void ClearingTheSaveZeroesEveryBestRecord()
        {
            var records = new ChallengeRecords(recordPrefix);
            records.Submit(Challenges.RocketOnly, 800);
            records.Clear();
            Assert.AreEqual(0, records.Best(Challenges.RocketOnly));
            Assert.AreEqual(0, new ChallengeRecords(recordPrefix).Best(Challenges.RocketOnly));
            Assert.IsTrue(records.Submit(Challenges.RocketOnly, 10), "a fresh record counts as a new best");
        }

        [Test]
        public void ScoresOfZeroNeverCountAsARecord()
        {
            var records = new ChallengeRecords(recordPrefix);
            Assert.IsFalse(records.Submit(Challenges.RocketOnly, 0));
            Assert.AreEqual(0, records.Best(Challenges.RocketOnly));
        }
    }
}
