using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TNDA.Prototype.Tests
{
    // The 不被撞毀 challenge on the public match: four cars, the player against three 追王, last 120 s without a wreck.
    // Bare cars in a preview scene, like ChallengeTests.
    public class NoWreckChallengeTests
    {
        private const float Dt = 1f / 60;
        private static readonly ArcadeInput Idle = new ArcadeInput();
        private Scene scene;
        private ArcadeVehicle[] cars;
        private DemolitionMatch match;

        [SetUp]
        public void CreateMatch()
        {
            scene = EditorSceneManager.NewPreviewScene();
            cars = new ArcadeVehicle[4];
            for (int i = 0; i < cars.Length; i++)
            {
                var root = new GameObject("Car " + i);
                SceneManager.MoveGameObjectToScene(root, scene);
                cars[i] = root.AddComponent<ArcadeVehicle>();
                cars[i].index = i;
            }
            var tuning = new MatchTuning { randomPropSpots = false };
            Challenges.NoWreck.ApplyRules(tuning);
            match = new DemolitionMatch(cars, new BreakableProp[0], new ArenaPickup[0], new[] { new ArcadeProjectile() }, tuning);
            match.Reset(1);
        }

        [TearDown]
        public void CleanUp() => EditorSceneManager.ClosePreviewScene(scene);

        [Test]
        public void WreckingThePlayerFailsTheChallengeAtOnce()
        {
            cars[0].spawnProtection = 999; // nothing wrecks the player on the way
            for (int i = 0; i < 60 * 30; i++) match.Tick(Dt, Idle, Idle); // well past the grace period, mid-round
            match.ClearEvents();
            cars[0].spawnProtection = 0;
            cars[0].shieldSeconds = 0;

            match.ApplyDamage(cars[0], 2);

            Assert.IsTrue(match.Finished, "the run ends on the wreck, not at 120 s");
            Assert.IsFalse(match.Survived);
            Assert.AreEqual(0, Challenges.NoWreck.ScoreOf(match, cars[0]), "a failed run scores nothing");
            var end = match.Events.OfType<MatchEnded>().Single();
            Assert.IsTrue(end.Winner.computer);
            Assert.AreEqual(4, cars[0].FinalPlace, "the wrecked player is last for the gold reward");
        }

        [Test]
        public void LastingTo120SecondsPassesAndScoresTheDurabilityLeft()
        {
            cars[0].health = .73f;
            cars[0].spawnProtection = 999;
            for (int i = 1; i < cars.Length; i++) cars[i].score = 900 + i * 100; // outscoring the player does not matter
            while (match.Elapsed < 119.9f) match.Tick(Dt, Idle, Idle);
            Assert.IsFalse(match.Finished);

            while (!match.Finished && match.Elapsed < 121) match.Tick(Dt, Idle, Idle);

            Assert.IsTrue(match.Finished);
            Assert.IsTrue(match.Survived);
            Assert.AreEqual(0, match.Champion);
            Assert.AreEqual(1, cars[0].FinalPlace);
            Assert.AreEqual(73, Challenges.NoWreck.ScoreOf(match, cars[0]));
        }

        [Test]
        public void AllThreeComputersAreHuntersThatChaseOnlyThePlayerAfterTheGracePeriod()
        {
            var computers = cars.Where(c => c.computer).ToArray();
            Assert.AreEqual(3, computers.Length);
            foreach (var ai in computers) Assert.AreEqual(AiPersonality.Hunter, ai.Ai.Personality, ai.name);

            cars[0].spawnProtection = 999;
            while (match.Elapsed < 9.9f)
            {
                match.Tick(Dt, Idle, Idle);
                foreach (var ai in computers) Assert.IsNull(ai.Ai.Target, ai.name + " at " + match.Elapsed);
            }
            Assert.AreEqual(1, cars[0].health, "the grace period still spares the player");

            var chased = computers.ToDictionary(ai => ai, ai => false);
            while (match.Elapsed < 20)
            {
                match.Tick(Dt, Idle, Idle);
                foreach (var ai in computers)
                {
                    var target = ai.Ai.Target;
                    if (target == null) continue;
                    Assert.AreSame(cars[0], target, ai.name + " only ever chases the player");
                    chased[ai] = true;
                }
            }
            foreach (var ai in computers) Assert.IsTrue(chased[ai], ai.name + " chased the player");
        }

        [Test]
        public void AComputerWithACherryHuntsThePlayerNotAnotherComputer()
        {
            cars[0].spawnProtection = 999;
            while (match.Elapsed < 10.5f) match.Tick(Dt, Idle, Idle);
            var hunter = cars[1];
            cars[2].score = 9000; // a juicier computer, right next to it
            cars[2].position = hunter.position + Vector2.right * .8f;
            cars[0].position = hunter.position + Vector2.left * 3;
            hunter.GiveCherry(8);
            for (int i = 0; i < 30; i++)
            {
                match.Tick(Dt, Idle, Idle);
                if (hunter.Ai.Target != null) Assert.AreSame(cars[0], hunter.Ai.Target, "frame " + i);
            }
            Assert.AreSame(cars[0], hunter.Ai.Target);
        }

        [Test]
        public void TheArcadeMatchKeepsItsMixedPersonalities()
        {
            var tuning = new MatchTuning();
            Challenges.NoWreck.ApplyRules(tuning);
            tuning.ResetChallengeSwitches();
            Assert.IsFalse(tuning.wreckEndsRound);
            Assert.IsFalse(tuning.allAiHuntThePlayer);
            var arcade = new DemolitionMatch(cars, new BreakableProp[0], new ArenaPickup[0], new ArcadeProjectile[0], tuning);
            arcade.Reset(1);
            CollectionAssert.AreEquivalent(new[] { AiPersonality.Wanderer, AiPersonality.Hunter, AiPersonality.Bumper },
                cars.Where(c => c.computer).Select(c => c.Ai.Personality));
        }
    }
}
