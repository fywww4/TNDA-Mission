using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TNDA.Prototype.Tests
{
    // Campaign stage rules (ticket 15) on the public match: the tutorial's steps and fight, and the boss stage's teams,
    // boss and endings. Bare cars in a preview scene, like the challenge tests.
    public class CampaignMatchTests
    {
        private const float Dt = 1f / 60;
        private static readonly ArcadeInput Idle = new ArcadeInput();
        private Scene scene;
        private ArcadeVehicle[] cars;
        private DemolitionMatch match;
        private MatchTuning tuning;
        private TutorialCoach coach;
        private CampaignStage stage;

        private void Create(CampaignStage forStage)
        {
            stage = forStage;
            scene = EditorSceneManager.NewPreviewScene();
            cars = new ArcadeVehicle[stage.CarCount];
            for (int i = 0; i < cars.Length; i++)
            {
                var root = new GameObject("Car " + i);
                SceneManager.MoveGameObjectToScene(root, scene);
                cars[i] = root.AddComponent<ArcadeVehicle>();
                cars[i].index = i;
            }
            coach = stage.Tutorial ? new TutorialCoach() : null;
            tuning = new MatchTuning { randomPropSpots = false };
            ApplyRules();
            match = new DemolitionMatch(cars, new BreakableProp[0], new ArenaPickup[0], new[] { new ArcadeProjectile(), new ArcadeProjectile() }, tuning);
            match.Reset(1);
        }

        // DemolitionGame re-applies the stage's rules before every use of the match; the tests do it by hand.
        private void ApplyRules()
        {
            tuning.ResetChallengeSwitches();
            stage.ApplyRules(tuning, coach);
        }

        private void Tick(ArcadeInput p1 = default)
        {
            ApplyRules();
            match.Tick(Dt, p1, Idle);
            coach?.Observe(cars[0], match.Events, match.Elapsed);
            ApplyRules();
            match.ClearEvents();
        }

        [TearDown]
        public void CleanUp() => EditorSceneManager.ClosePreviewScene(scene);

        [Test]
        public void TheProgressOpensEachStageOnceTheOneBeforeIsCleared()
        {
            var progress = new CampaignProgress("TNDA.Tests.Campaign.");
            progress.Clear();
            try
            {
                Assert.IsTrue(progress.IsUnlocked(Campaign.Tutorial));
                Assert.IsFalse(progress.IsUnlocked(Campaign.Pyramid));
                Assert.IsFalse(progress.IsUnlocked(Campaign.BossFight));
                progress.MarkCleared(Campaign.Tutorial);
                Assert.IsTrue(progress.IsUnlocked(Campaign.Pyramid));
                Assert.IsFalse(progress.IsUnlocked(Campaign.BossFight));
                progress.MarkCleared(Campaign.Pyramid);
                Assert.IsTrue(progress.IsUnlocked(Campaign.BossFight));
                Assert.AreSame(Campaign.BossFight, Campaign.After(Campaign.Pyramid));
                Assert.IsNull(Campaign.After(Campaign.BossFight));
                progress.Clear();
                Assert.IsFalse(progress.IsCleared(Campaign.Tutorial));
                Assert.IsFalse(progress.IsUnlocked(Campaign.Pyramid));
            }
            finally { progress.Clear(); }
        }

        [Test]
        public void TheTutorialStepsGoInOrderAndAStepDoneEarlyCountsWhenItsTurnComes()
        {
            Create(Campaign.Tutorial);
            Assert.AreEqual(4, cars.Length);
            Assert.IsTrue(cars.Skip(1).All(c => c.computer && c.Ai.Personality == AiPersonality.Bumper));
            Assert.AreEqual(TutorialStep.Gas, coach.Step);
            var player = cars[0];
            // A crate picked up first only counts later.
            coach.Observe(player, new MatchEvent[] { new ItemPicked(player, ArenaItem.Rocket) }, 0);
            Assert.AreEqual(TutorialStep.Gas, coach.Step);
            player.speed = 2;
            coach.Observe(player, new MatchEvent[0], 0);
            Assert.AreEqual(TutorialStep.Steer, coach.Step);
            for (int i = 0; i < 40; i++) { player.heading += 10; coach.Observe(player, new MatchEvent[0], 0); }
            Assert.AreEqual(TutorialStep.Brake, coach.Step);
            player.speed = -1;
            coach.Observe(player, new MatchEvent[0], 0);
            Assert.AreEqual(TutorialStep.Ram, coach.Step);
            coach.Observe(player, new MatchEvent[] { new CarRammed(cars[1], player, 2, 1, true, ImpactKind.Front, false) }, 0);
            Assert.AreEqual(TutorialStep.Ram, coach.Step, "being rammed is not ramming");
            coach.Observe(player, new MatchEvent[] { new CarRammed(player, cars[1], 2, 1, true, ImpactKind.Front, false) }, 0);
            Assert.AreEqual(TutorialStep.Item, coach.Step, "the crate picked up earlier is already done");
            Assert.IsFalse(coach.Fighting);
            Assert.IsTrue(coach.Observe(player, new MatchEvent[] { new ItemUsed(player, ArenaItem.Rocket) }, 12.5f));
            Assert.IsTrue(coach.Fighting);
            Assert.AreEqual(12.5f, coach.FightStartedAt);
        }

        [Test]
        public void DuringTheStepsTheComputersCannotHurtThePlayerAndNobodyIsKnockedOut()
        {
            Create(Campaign.Tutorial);
            for (int i = 0; i < 60 * 3; i++) Tick();
            Assert.IsTrue(tuning.aiSparesPlayers);
            foreach (var car in cars) { car.spawnProtection = 0; car.shieldSeconds = 0; }
            // A computer driving hard into the player bumps it but does no damage.
            cars[0].position = Vector2.zero; cars[0].heading = 0; cars[0].speed = 0;
            cars[1].position = new Vector2(-.6f, 0); cars[1].heading = 0; cars[1].speed = 3;
            cars[2].position = new Vector2(3, -3); cars[3].position = new Vector2(-3, -3);
            Tick();
            Assert.AreEqual(1, cars[0].health, 1e-4f, "the match spares the player during the steps");
            match.ApplyDamage(cars[1], 2);
            Assert.IsFalse(cars[1].eliminated, "a computer wrecked while learning comes back");
            Assert.IsFalse(match.Finished);
            for (int i = 0; i < 60 * 200; i++) Tick();
            Assert.IsFalse(match.Finished, "no clock while the steps are not done");
        }

        private void FinishTheSteps()
        {
            var player = cars[0];
            coach.Observe(player, new MatchEvent[] {
                new ItemPicked(player, ArenaItem.Rocket), new ItemUsed(player, ArenaItem.Rocket),
                new CarRammed(player, cars[1], 2, 1, true, ImpactKind.Front, false) }, match.Elapsed);
            player.speed = 2; coach.Observe(player, new MatchEvent[0], match.Elapsed);
            for (int i = 0; i < 40; i++) { player.heading += 10; coach.Observe(player, new MatchEvent[0], match.Elapsed); }
            player.speed = -1; coach.Observe(player, new MatchEvent[0], match.Elapsed);
            Assert.IsTrue(coach.Fighting);
            player.speed = 0;
            ApplyRules();
        }

        [Test]
        public void InTheFightAWreckedComputerIsOutAndWreckingAllThreeClearsTheStage()
        {
            Create(Campaign.Tutorial);
            Tick();
            FinishTheSteps();
            Assert.IsTrue(tuning.wrecksKnockOut);
            foreach (var car in cars) { car.spawnProtection = 0; car.shieldSeconds = 0; }
            match.ApplyDamage(cars[1], 2);
            Assert.IsTrue(cars[1].eliminated, "a wrecked computer is out for good");
            Assert.IsTrue(match.Events.OfType<VehicleEliminated>().Single().Wrecked);
            Assert.IsFalse(match.Finished);
            match.ApplyDamage(cars[2], 2);
            match.ApplyDamage(cars[3], 2);
            Assert.IsTrue(match.Finished);
            Assert.IsTrue(match.Survived);
            Assert.AreEqual(0, match.Champion);
        }

        [Test]
        public void InTheFightAWreckedPlayerFailsTheStage()
        {
            Create(Campaign.Tutorial);
            Tick();
            FinishTheSteps();
            cars[0].spawnProtection = 0;
            match.ApplyDamage(cars[0], 2);
            Assert.IsTrue(match.Finished);
            Assert.IsFalse(match.Survived);
        }

        [Test]
        public void TheBossStageIsThreeAgainstThreeAndTeammatesNeverHurtEachOther()
        {
            Create(Campaign.BossFight);
            Assert.AreEqual(6, cars.Length);
            Assert.AreEqual(new[] { 1, 1, 1, 2, 2, 2 }, cars.Select(c => c.team).ToArray());
            Assert.AreEqual(AiPersonality.Boss, cars[Campaign.BossSeat].Ai.Personality);
            Assert.AreEqual(CabinetText.AllyName, cars[1].DriverName);
            Assert.AreEqual(CabinetText.BossName, cars[3].DriverName);
            Assert.AreEqual(CabinetText.MinionName, cars[4].DriverName);
            Assert.AreEqual("P1", cars[0].DriverName);
            foreach (var car in cars) car.spawnProtection = 0;
            Assert.IsFalse(VehicleHarmPolicy.CanHarm(cars[1], cars[0], 99, 0), "an ally cannot hurt the player");
            Assert.IsFalse(VehicleHarmPolicy.CanHarm(cars[0], cars[2], 99, 0), "nor the player an ally");
            Assert.IsFalse(VehicleHarmPolicy.CanHarm(cars[4], cars[3], 99, 0), "nor a minion its boss");
            Assert.IsTrue(VehicleHarmPolicy.CanHarm(cars[0], cars[3], 99, 0));
            Assert.IsTrue(VehicleHarmPolicy.CanHarm(cars[4], cars[0], 99, 0));
            // A teammate driving hard into the player bumps but does no damage.
            cars[0].position = new Vector2(0, 0); cars[0].heading = 0; cars[0].speed = 0;
            cars[1].position = new Vector2(-.6f, 0); cars[1].heading = 0; cars[1].speed = 3;
            for (int i = 3; i < 6; i++) cars[i].position = new Vector2(3, -3 + i * .1f);
            match.Tick(Dt, Idle, Idle);
            Assert.AreEqual(1, cars[0].health, 1e-4f);
        }

        [Test]
        public void TheBossIsBigHeavyAndArmoured()
        {
            Create(Campaign.BossFight);
            var boss = cars[Campaign.BossSeat];
            Assert.AreEqual(tuning.bossSize, boss.size);
            Assert.Greater(boss.Radius, cars[0].Radius * 1.5f);
            Assert.AreEqual(1, cars[0].size);
            boss.spawnProtection = 0;
            match.ApplyDamage(boss, .5f);
            Assert.AreEqual(1 - .5f / tuning.bossArmor, boss.health, 1e-4f, "armour cuts every hit");
            // It touches from further away than an ordinary car, and pushes the lighter car aside more.
            foreach (var car in cars) { car.position = new Vector2(5, 5) + Vector2.right * car.index; car.spawnProtection = 99; }
            boss.position = Vector2.zero;
            cars[0].position = new Vector2(.75f, 0);
            match.Tick(Dt, Idle, Idle);
            Assert.Greater(cars[0].position.x - .75f, -boss.position.x, "the player is shoved further than the boss");
            Assert.Greater(cars[0].SlideVelocity.x, -boss.SlideVelocity.x);
        }

        [Test]
        public void AlliesAreHalfAgainAsStrongAndTheBossIsHalved()
        {
            Create(Campaign.BossFight);
            var boss = cars[Campaign.BossSeat];
            Assert.AreEqual(2.5f, boss.armor, 1e-4f);
            Assert.AreEqual(1.25f, boss.mass, 1e-4f);
            Assert.AreEqual(14, tuning.bossRocketSeconds, 1e-4f);
            foreach (int ally in new[] { 1, 2 })
            {
                Assert.AreEqual(1.5f, cars[ally].armor, 1e-4f);
                Assert.AreEqual(1.5f, cars[ally].mass, 1e-4f);
                Assert.AreEqual(1.5f, cars[ally].power, 1e-4f);
            }
            // The player, the minions and the boss keep an ordinary car's ram damage.
            foreach (int seat in new[] { 0, 3, 4, 5 }) Assert.AreEqual(1, cars[seat].power, 1e-4f);
            foreach (int seat in new[] { 0, 4, 5 }) Assert.AreEqual(1, cars[seat].armor, 1e-4f);
            // An ally takes two thirds of a hit.
            cars[1].spawnProtection = 0;
            match.ApplyDamage(cars[1], .3f);
            Assert.AreEqual(1 - .3f / 1.5f, cars[1].health, 1e-4f);
        }

        [Test]
        public void WreckingTheBossClearsTheStage()
        {
            Create(Campaign.BossFight);
            var boss = cars[Campaign.BossSeat];
            boss.spawnProtection = 0;
            match.ApplyDamage(boss, 99);
            Assert.IsTrue(match.Finished);
            Assert.IsTrue(match.Survived);
            Assert.AreEqual(0, match.Champion);
        }

        [Test]
        public void WreckedAlliesAndMinionsAreOutButTheStageGoesOn()
        {
            Create(Campaign.BossFight);
            foreach (var car in cars) car.spawnProtection = 0;
            match.ApplyDamage(cars[1], 99);
            match.ApplyDamage(cars[4], 99);
            Assert.IsTrue(cars[1].eliminated && cars[4].eliminated);
            Assert.IsFalse(match.Finished);
        }

        [Test]
        public void RunningOutOfTimeOrBeingWreckedLosesTheBossStage()
        {
            Create(Campaign.BossFight);
            cars[0].spawnProtection = 999;
            // The allies could wreck the boss within the time; keep it untouchable to see the clock alone.
            cars[Campaign.BossSeat].spawnProtection = 999;
            cars[0].score = 99999; // outscoring the boss does not matter
            while (!match.Finished && match.Elapsed < Campaign.BossSeconds + 1) Tick();
            Assert.IsTrue(match.Finished);
            Assert.IsFalse(match.Survived, "time up");
            Assert.AreEqual(Campaign.BossSeat, match.Champion);
            CleanUp();

            Create(Campaign.BossFight);
            cars[0].spawnProtection = 0;
            match.ApplyDamage(cars[0], 99);
            Assert.IsTrue(match.Finished);
            Assert.IsFalse(match.Survived, "the player wrecked");
        }

        [Test]
        public void TheBossGetsARocketEveryFewSecondsAndChargesInItsRhythm()
        {
            Create(Campaign.BossFight);
            var boss = cars[Campaign.BossSeat];
            Assert.AreEqual(ArenaItem.None, boss.item);
            bool charged = false, armed = false;
            for (float t = 0; t < tuning.bossRocketSeconds + .2f && !armed; t += Dt)
            {
                ApplyRules();
                match.Tick(Dt, Idle, Idle);
                // It may fire the rocket on the very frame it gets one.
                armed = boss.item == ArenaItem.Rocket || match.Events.OfType<ItemUsed>().Any(e => e.Car == boss);
                match.ClearEvents();
                charged |= boss.Ai.Charging;
            }
            Assert.IsTrue(armed, "a rocket within bossRocketSeconds");
            Assert.IsTrue(charged, "it charged at least once within the first few seconds");
            Assert.Greater(match.AiTopSpeed(boss), 0);
        }
    }
}
