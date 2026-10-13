using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TNDA.Prototype.Tests
{
    // The match rules run with bare cars in a preview scene: no GAME3 scene, Canvas, camera or audio.
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
                var body = new GameObject("Body").AddComponent<SpriteRenderer>();
                body.transform.SetParent(root.transform, false);
                cars[i] = root.AddComponent<ArcadeVehicle>();
                cars[i].index = i;
                cars[i].body = body;
            }
            var props = new[] {
                new BreakableProp { point = new Vector2(-1.5f, -2.5f) },
                new BreakableProp { point = new Vector2(1.5f, -2.5f), points = 75 }
            };
            rockets = new[] { new ArcadeProjectile(), new ArcadeProjectile() };
            match = new DemolitionMatch(cars, props, new ArenaPickup[0], rockets, new MatchTuning());
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

        private MatchEvent[] RuleEvents() => match.Events.Where(e => !(e is NoticeCue || e is ToneCue || e is HitCue)).ToArray();

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

            var events = RuleEvents();
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

            var events = RuleEvents();
            Assert.AreSame(target, ((ShieldBlocked)events[0]).Car);
            Assert.AreSame(target, ((RocketHit)events[1]).Victim);
            Assert.AreEqual(2, events.Length);
            Assert.AreEqual(1, target.health);
            Assert.AreEqual(0, cars[0].score);
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
