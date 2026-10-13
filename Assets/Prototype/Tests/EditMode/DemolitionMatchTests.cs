using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TNDA.Prototype.Tests
{
    // The match rules run with bare cars in a preview scene: no GAME3 scene, Canvas, camera or audio. The cars have
    // no sprite either, so any rule that reached for a view object would throw.
    public class DemolitionMatchTests
    {
        private const float Dt = 1f / 60;
        private static readonly ArcadeInput Idle = new ArcadeInput();
        private Scene scene;
        private ArcadeVehicle[] cars;
        private ArcadeProjectile[] rockets;
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
            var props = new[] {
                new BreakableProp { point = new Vector2(-1.5f, -2.5f) },
                new BreakableProp { point = new Vector2(1.5f, -2.5f), points = 75 }
            };
            var pickups = new[] { new ArenaPickup { point = new Vector2(0, 1), item = ArenaItem.Shield } };
            rockets = new[] { new ArcadeProjectile(), new ArcadeProjectile() };
            match = new DemolitionMatch(cars, props, pickups, rockets, new MatchTuning());
        }

        [TearDown]
        public void CloseScene() => EditorSceneManager.ClosePreviewScene(scene);

        // P1 fires along y = -1 at car 3; nobody drives, so only the rocket moves.
        private ArcadeVehicle FireP1RocketAtCar3(System.Action<ArcadeVehicle> prepare = null)
        {
            match.Reset(1);
            foreach (var car in cars) { car.computer = false; car.spawnProtection = 0; }
            var target = cars[3];
            target.position = new Vector2(1, -1);
            prepare?.Invoke(target);
            rockets[0].Spawn(new Vector2(-.5f, -1), new Vector2(6, 0), 0);
            for (int f = 0; f < 20 && rockets[0].active; f++) match.Tick(Dt, Idle, Idle);
            return target;
        }

        [Test]
        public void RocketHitReportsAttackerVictimAndPoint()
        {
            var target = FireP1RocketAtCar3();

            var hit = match.Events.OfType<RocketHit>().Single();
            Assert.AreSame(cars[0], hit.Attacker);
            Assert.AreSame(target, hit.Victim);
            Assert.That(Vector2.Distance(hit.Point, new Vector2(1, -1)), Is.LessThan(.001f));
            Assert.That(target.health, Is.EqualTo(.62f).Within(.001f));
            Assert.AreEqual(120, cars[0].score);
            Assert.IsFalse(rockets[0].active);
        }

        [Test]
        public void LethalRocketReportsTheWreckThenTheBonus()
        {
            // Any rocket wrecks a cherry car.
            var target = FireP1RocketAtCar3(car => { car.score = 400; car.GiveCherry(8); });
            Assert.IsFalse(target.alive);

            var events = match.Events.ToArray();
            Assert.AreEqual(3, events.Length);
            var wreck = (VehicleWrecked)events[0];
            Assert.AreSame(target, wreck.Car);
            Assert.AreEqual(250, wreck.Deducted);
            var bonus = (WreckBonus)events[1];
            Assert.AreSame(cars[0], bonus.Attacker);
            Assert.AreEqual(300, bonus.Points);
            Assert.IsInstanceOf<RocketHit>(events[2]);
            Assert.AreEqual(420, cars[0].score);
        }

        [Test]
        public void ShieldedVictimReportsTheBlockAndScoresNothing()
        {
            var target = FireP1RocketAtCar3(car => car.shieldSeconds = 6);

            var events = match.Events.ToArray();
            Assert.AreSame(target, ((ShieldBlocked)events[0]).Car);
            Assert.AreSame(target, ((RocketHit)events[1]).Victim);
            Assert.AreEqual(2, events.Length);
            Assert.AreEqual(1, target.health);
            Assert.AreEqual(0, cars[0].score);
        }

        // P1 drives side-on into car 3, which has almost no durability left.
        private void P1RamsCar3(float victimScore, float victimHealth = .01f)
        {
            match.Reset(1);
            foreach (var car in cars) { car.computer = false; car.spawnProtection = 0; }
            cars[0].position = new Vector2(0, -1); cars[0].heading = 0; cars[0].speed = 2.8f;
            cars[3].position = new Vector2(.55f, -1); cars[3].heading = 90;
            cars[3].health = victimHealth;
            cars[3].score = victimScore;
            match.Tick(Dt, Idle, Idle);
        }

        [Test]
        public void RammingACarToDeathDeductsFromTheVictimAndRewardsTheRammer()
        {
            P1RamsCar3(400);

            Assert.IsFalse(cars[3].alive);
            Assert.AreEqual(150, cars[3].score);
            var wreck = match.Events.OfType<VehicleWrecked>().Single();
            Assert.AreSame(cars[3], wreck.Car);
            Assert.AreEqual(250, wreck.Deducted);
            var bonus = match.Events.OfType<WreckBonus>().Single();
            Assert.AreSame(cars[0], bonus.Attacker);
            Assert.AreEqual(300, bonus.Points);
            Assert.That(cars[0].score, Is.GreaterThanOrEqualTo(300));
        }

        [Test]
        public void TheWreckPenaltyNeverTakesTheScoreBelowZero()
        {
            P1RamsCar3(100);

            Assert.AreEqual(0, cars[3].score);
            Assert.AreEqual(100, match.Events.OfType<VehicleWrecked>().Single().Deducted);
        }

        [Test]
        public void AWreckStaysVisibleWhileItSpinsOffAndReturnsWhenItRespawns()
        {
            P1RamsCar3(0);
            Assert.IsTrue(cars[3].Visible);

            for (int f = 0; f < 60 && cars[3].IsSpinningOut; f++) match.AdvanceSpinOuts(Dt, Vector2.zero, 6);
            Assert.IsFalse(cars[3].Visible);

            for (int f = 0; f < 3 * 60 + 2 && !cars[3].alive; f++) match.Tick(Dt, Idle, Idle);
            Assert.IsTrue(cars[3].alive);
            Assert.IsTrue(cars[3].Visible);
        }

        // Humans with no input only coast, so set pieces stay where the test puts them.
        private void ResetWithHumansOnly()
        {
            match.Reset(1);
            foreach (var car in cars) { car.computer = false; car.spawnProtection = 0; }
        }

        [Test]
        public void DrivingFastIntoAPropSmashesItForItsPoints()
        {
            ResetWithHumansOnly();
            cars[0].position = new Vector2(-2.1f, -2.5f); cars[0].heading = 0; cars[0].speed = 2.8f;
            match.Tick(Dt, Idle, Idle);

            var smash = match.Events.OfType<PropSmashed>().Single();
            Assert.AreSame(cars[0], smash.Car);
            Assert.AreEqual(100, smash.Points);
            Assert.IsFalse(smash.ByRocket);
            Assert.AreEqual(100, cars[0].score);
        }

        [Test]
        public void ARocketSmashesAPropForItsOwner()
        {
            ResetWithHumansOnly();
            rockets[0].Spawn(new Vector2(.5f, -2.5f), new Vector2(6, 0), 0);
            for (int f = 0; f < 20 && rockets[0].active; f++) match.Tick(Dt, Idle, Idle);

            var smash = match.Events.OfType<PropSmashed>().Single();
            Assert.AreSame(cars[0], smash.Car);
            Assert.AreEqual(75, smash.Points);
            Assert.IsTrue(smash.ByRocket);
            Assert.AreEqual(75, cars[0].score);
        }

        private static readonly ArcadeInput ItemButton = new ArcadeInput { action = true, actionPressed = true };

        [Test]
        public void DrivingOverACrateReportsTheItemPickedUp()
        {
            ResetWithHumansOnly();
            cars[0].position = new Vector2(0, 1);
            match.Tick(Dt, Idle, Idle);

            var picked = match.Events.OfType<ItemPicked>().Single();
            Assert.AreSame(cars[0], picked.Car);
            Assert.AreEqual(ArenaItem.Shield, picked.Item);
            Assert.AreEqual(ArenaItem.Shield, cars[0].item);
        }

        [TestCase(ArenaItem.Shield)]
        [TestCase(ArenaItem.Rocket)]
        [TestCase(ArenaItem.Cherry)]
        public void PressingTheItemButtonReportsTheItemUsed(ArenaItem item)
        {
            ResetWithHumansOnly();
            cars[0].item = item;
            match.Tick(Dt, ItemButton, Idle);

            var used = match.Events.OfType<ItemUsed>().Single();
            Assert.AreSame(cars[0], used.Car);
            Assert.AreEqual(item, used.Item);
            Assert.AreEqual(ArenaItem.None, cars[0].item);
        }

        [Test]
        public void ARocketWithBothRocketsFlyingIsKeptAndReported()
        {
            ResetWithHumansOnly();
            rockets[0].Spawn(new Vector2(-1, 0), new Vector2(6, 0), 1);
            rockets[1].Spawn(new Vector2(-1, -.5f), new Vector2(6, 0), 1);
            cars[0].item = ArenaItem.Rocket;
            match.Tick(Dt, ItemButton, Idle);

            Assert.AreSame(cars[0], match.Events.OfType<RocketsBusy>().Single().Car);
            Assert.IsEmpty(match.Events.OfType<ItemUsed>());
            Assert.AreEqual(ArenaItem.Rocket, cars[0].item);
        }

        [Test]
        public void ARamThatDentsTheVictimReportsTheImpact()
        {
            P1RamsCar3(0, 1);

            var ram = match.Events.OfType<CarRammed>().Single();
            Assert.AreSame(cars[0], ram.Attacker);
            Assert.AreSame(cars[3], ram.Victim);
            Assert.AreEqual(2, ram.Multiplier, "a side impact counts double");
            Assert.That(ram.Impact, Is.GreaterThanOrEqualTo(1));
            Assert.IsTrue(ram.VictimHurt);
            Assert.IsTrue(cars[3].alive);
        }

        [Test]
        public void ARamThatWrecksTheVictimIsNotReportedAsHurt()
        {
            P1RamsCar3(0);

            Assert.IsFalse(match.Events.OfType<CarRammed>().Single().VictimHurt);
        }

        [Test]
        public void TouchingACherryCarWrecksTheOtherCar()
        {
            ResetWithHumansOnly();
            cars[0].position = new Vector2(0, -1); cars[0].GiveCherry(8);
            cars[3].position = new Vector2(.55f, -1);
            match.Tick(Dt, Idle, Idle);

            var strike = match.Events.OfType<CherryStrike>().Single();
            Assert.AreSame(cars[0], strike.Attacker);
            Assert.AreSame(cars[3], strike.Victim);
            Assert.IsTrue(strike.Wrecked);
            Assert.IsFalse(cars[3].alive);
        }

        [Test]
        public void AComputerCherryCarCannotWreckAPlayerDuringTheGracePeriod()
        {
            ResetWithHumansOnly();
            cars[3].computer = true;
            cars[3].position = new Vector2(.55f, -1); cars[3].GiveCherry(8);
            cars[0].position = new Vector2(0, -1);
            match.Tick(Dt, Idle, Idle);

            var strike = match.Events.OfType<CherryStrike>().Single();
            Assert.AreSame(cars[3], strike.Attacker);
            Assert.IsFalse(strike.Wrecked);
            Assert.IsTrue(cars[0].alive);
        }

        [Test]
        public void HittingAWallFastIsReportedButCreepingIntoItIsNot()
        {
            ResetWithHumansOnly();
            cars[0].position = new Vector2(4.34f, -1); cars[0].heading = 0; cars[0].speed = 2.8f;
            cars[1].position = new Vector2(-4.34f, -1); cars[1].heading = 180; cars[1].speed = .5f;
            match.Tick(Dt, Idle, Idle);

            Assert.AreSame(cars[0], match.Events.OfType<WallHit>().Single().Car);
        }

        private ArcadeVehicle EliminateWith(float[] scores, float[] health)
        {
            match.Reset(1);
            for (int i = 0; i < cars.Length; i++) { cars[i].score = scores[i]; cars[i].health = health[i]; }
            match.EliminateLowest();
            var eliminated = match.Events.OfType<VehicleEliminated>().Single();
            Assert.AreEqual(4, eliminated.Place);
            Assert.IsTrue(eliminated.Car.eliminated);
            return eliminated.Car;
        }

        [Test]
        public void TimedEliminationTakesTheLowestScore()
        {
            Assert.AreSame(cars[1], EliminateWith(new float[] { 500, 100, 300, 200 }, new float[] { .2f, 1, .2f, .2f }));
        }

        [Test]
        public void TiedScoresEliminateTheLowerDurability()
        {
            Assert.AreSame(cars[1], EliminateWith(new float[] { 100, 100, 100, 100 }, new float[] { 1, .4f, .5f, 1 }));
        }

        [Test]
        public void TiedScoresAndDurabilityEliminateTheHigherIndex()
        {
            Assert.AreSame(cars[2], EliminateWith(new float[] { 100, 100, 100, 300 }, new float[] { 1, 1, 1, 1 }));
        }

        [Test]
        public void TheClockEliminatesTheLowestAt60And90SecondsNotBefore()
        {
            ResetWithHumansOnly();
            float[] scores = { 400, 300, 200, 100 };
            for (int i = 0; i < cars.Length; i++) cars[i].score = scores[i];
            while (match.Elapsed < 59.9f) match.Tick(Dt, Idle, Idle);
            Assert.IsEmpty(match.Events.OfType<VehicleEliminated>());

            while (match.Elapsed < 90.1f) match.Tick(Dt, Idle, Idle);
            CollectionAssert.AreEqual(new[] { cars[3], cars[2] }, match.Events.OfType<VehicleEliminated>().Select(e => e.Car));
            Assert.IsFalse(match.Finished);
        }

        [Test]
        public void ADemoMatchIsNeverEliminatedByTheClock()
        {
            match.Reset(0, true);
            while (match.Elapsed < 125) match.Tick(Dt, Idle, Idle);

            Assert.IsEmpty(match.Events.OfType<VehicleEliminated>());
            Assert.IsFalse(match.Finished);
        }

        [Test]
        public void TheLastCarStandingIsChampionAndAHumanChampionSurvives()
        {
            match.Reset(1);
            float[] scores = { 900, 300, 100, 600 };
            for (int i = 0; i < cars.Length; i++) cars[i].score = scores[i];
            for (int stage = 0; stage < 3; stage++) match.EliminateLowest();

            CollectionAssert.AreEqual(new[] { cars[2], cars[1], cars[3] }, match.Events.OfType<VehicleEliminated>().Select(e => e.Car));
            Assert.IsTrue(match.Finished);
            Assert.AreEqual(0, match.Champion);
            Assert.AreSame(cars[0], match.Events.OfType<MatchEnded>().Single().Winner);
            Assert.AreEqual(1, cars[0].FinalPlace);
            Assert.IsTrue(match.Survived);
        }

        [Test]
        public void AComputerChampionMeansThePlayerDidNotSurvive()
        {
            match.Reset(1);
            float[] scores = { 100, 300, 900, 600 };
            for (int i = 0; i < cars.Length; i++) cars[i].score = scores[i];
            for (int stage = 0; stage < 3; stage++) match.EliminateLowest();

            Assert.AreEqual(2, match.Champion);
            Assert.IsFalse(match.Survived);
            Assert.AreEqual(4, cars[0].FinalPlace);
        }

        [Test]
        public void AFullMatchEliminatesThreeCarsAndCrownsTheLast()
        {
            match.Reset(1);
            for (int f = 0; f < 121 * 60 && !match.Finished; f++) match.Tick(Dt, Idle, Idle);

            Assert.IsTrue(match.Finished);
            var places = match.Events.OfType<VehicleEliminated>().Select(e => e.Place).ToArray();
            CollectionAssert.AreEqual(new[] { 4, 3, 2 }, places);
            var end = match.Events.OfType<MatchEnded>().Single();
            Assert.AreEqual(1, end.Winner.FinalPlace);
            Assert.IsFalse(end.Winner.eliminated);
            Assert.AreSame(end.Winner, cars[match.Champion]);
        }
    }
}
