using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TNDA.Prototype.Tests
{
    public class ObstacleTests
    {
        private Scene scene;
        private ArcadeVehicle[] cars;
        private DemolitionMatch match;
        private ArenaObstacle obstacle;
        private static readonly ArcadeInput Gas = new ArcadeInput { throttle = 1 };

        [SetUp]
        public void SetUp()
        {
            scene = EditorSceneManager.NewPreviewScene();
            cars = new ArcadeVehicle[4];
            for (int i = 0; i < cars.Length; i++)
            {
                var go = new GameObject("Car " + i);
                SceneManager.MoveGameObjectToScene(go, scene);
                cars[i] = go.AddComponent<ArcadeVehicle>(); cars[i].index = i;
            }
            obstacle = new ArenaObstacle { kind = ObstacleKind.Fence, point = Vector2.zero, size = new Vector2(2, .4f) };
            match = new DemolitionMatch(cars, new BreakableProp[0], new ArenaPickup[0],
                new[] { new ArcadeProjectile() }, new MatchTuning { randomPropSpots = false,
                    arenaMin = new Vector2(-12, -10), arenaMax = new Vector2(12, 10) }, new[] { obstacle });
            match.Reset(1);
            foreach (var c in cars) { c.computer = false; c.position = new Vector2(8, -8 + c.index * 2); c.spawnProtection = 0; }
        }

        [TearDown]
        public void TearDown() => EditorSceneManager.ClosePreviewScene(scene);

        private void Approach(float speed)
        {
            match.Tuning.accelerationPerSecond = 0; // Keep the requested speed exact at contact.
            cars[0].position = new Vector2(0, -.7f); cars[0].heading = 90; cars[0].speed = speed;
            match.Tick(.2f, Gas, default);
        }

        [Test]
        public void SlowFenceContactBlocksButFastContactBreaksWithoutPoints()
        {
            Approach(2.09f);
            Assert.That(cars[0].position.y, Is.LessThan(-.5f), "The intact fence blocks the car's footprint.");
            Assert.That(obstacle.broken, Is.False);
            Approach(2.1f);
            Assert.That(obstacle.broken, Is.True);
            Assert.That(cars[0].score, Is.Zero, "Terrain is not a scoring prop.");
            Assert.That(cars[0].health, Is.EqualTo(1));
        }

        [TestCase(ObstacleKind.Pipe)]
        [TestCase(ObstacleKind.Wall)]
        public void SolidTerrainStopsEvenAFastCarWithoutDurabilityLoss(ObstacleKind kind)
        {
            obstacle.kind = kind;
            Approach(3.5f);
            Assert.That(cars[0].position.y, Is.LessThan(-.5f));
            Assert.That(cars[0].speed, Is.Zero);
            Assert.That(cars[0].stun, Is.GreaterThan(0));
            Assert.That(cars[0].health, Is.EqualTo(1));
            Assert.That(obstacle.broken, Is.False);
        }

        [Test]
        public void FenceWaitsForNearbyCarBeforeRegrowingAndResetsWithTheMatch()
        {
            Approach(3);
            cars[0].position = Vector2.zero; cars[0].speed = 0;
            for (int i = 0; i < 70; i++) match.Tick(.1f, default, default);
            Assert.That(obstacle.broken, Is.True, "Do not rebuild a fence under a car.");
            cars[0].position = new Vector2(5, 5);
            match.Tick(.1f, default, default);
            Assert.That(obstacle.broken, Is.False);
            Approach(3);
            match.Reset(1);
            Assert.That(obstacle.broken, Is.False);
        }

        [Test]
        public void RampLaunchesOnlyFastForwardCrossingsAndLandsBackOnTheFloor()
        {
            obstacle.kind = ObstacleKind.Ramp; obstacle.size = new Vector2(1.5f, 1.2f);
            cars[0].position = new Vector2(0, .4f); cars[0].heading = 90; cars[0].speed = 1;
            match.Tick(.2f, Gas, default);
            Assert.That(cars[0].Airborne, Is.False);
            cars[0].position = new Vector2(0, .4f); cars[0].speed = 3;
            match.Tick(.1f, Gas, default);
            Assert.That(cars[0].Airborne, Is.True);
            match.Tick(.2f, Gas, default);
            Assert.That(cars[0].AirHeight, Is.GreaterThan(.3f));
            for (int i = 0; i < 10; i++) match.Tick(.1f, Gas, default);
            Assert.That(cars[0].Airborne, Is.False);
            Assert.That(cars[0].AirHeight, Is.Zero);
            Assert.That(cars[0].speed, Is.GreaterThan(2), "Landing preserves driving speed.");
            cars[0].position = new Vector2(0, .7f); cars[0].heading = -90; cars[0].speed = 3;
            match.Tick(.1f, Gas, default);
            Assert.That(cars[0].Airborne, Is.False, "The back of a ramp is not another launch lip.");
        }

        [Test]
        public void AirborneCarPassesOverCarsCratesPropsOilAndSolidTerrainAndRocketsMissIt()
        {
            var prop = new BreakableProp { point = new Vector2(0, .6f) };
            var pickup = new ArenaPickup { point = prop.point };
            obstacle.kind = ObstacleKind.Wall; obstacle.point = prop.point;
            match = new DemolitionMatch(cars, new[] { prop }, new[] { pickup },
                new[] { new ArcadeProjectile(), new ArcadeProjectile() }, match.Tuning, new[] { obstacle });
            match.Reset(1);
            foreach (var c in cars) { c.computer = false; c.position = new Vector2(8, -8 + c.index * 2); c.spawnProtection = 0; }
            // Place these after Reset: this deliberately stacks hazards underneath the flying car.
            prop.point = pickup.point = new Vector2(0, .6f);
            cars[0].position = new Vector2(0, .4f); cars[0].heading = 90; cars[0].speed = 3;
            cars[0].Launch(.7f, .85f);
            cars[1].position = new Vector2(0, .7f);
            match.Rockets[0].Drop(new Vector2(0, .7f), 1);
            match.Rockets[1].Spawn(new Vector2(-.6f, 1.05f), Vector2.right * 6, 1);
            match.Tick(.1f, Gas, default);
            Assert.That(cars[0].position.y, Is.GreaterThan(.65f));
            Assert.That(cars[0].health, Is.EqualTo(1));
            Assert.That(cars[0].item, Is.EqualTo(ArenaItem.None));
            Assert.That(cars[0].IsSlipping, Is.False);
            Assert.That(prop.broken, Is.False);
            Assert.That(match.Events.OfType<CarRammed>(), Is.Empty);
            cars[0].Respawn(new Vector2(4, 4), 0);
            Assert.That(cars[0].Airborne, Is.False, "Lifecycle clears flight.");
        }

        [Test]
        public void PropsAndCratesInitiallyInsideTerrainMoveToClearFloor()
        {
            var prop = new BreakableProp { point = Vector2.zero };
            var pickup = new ArenaPickup { point = Vector2.zero };
            match = new DemolitionMatch(cars, new[] { prop }, new[] { pickup }, match.Rockets,
                match.Tuning, new[] { obstacle });
            match.Reset(1);
            Assert.That(obstacle.Contains(prop.point, .65f), Is.False);
            Assert.That(obstacle.Contains(pickup.point, .65f), Is.False);
            match.Tuning.randomPropSpots = true;
            for (int i = 0; i < 100; i++)
            {
                prop.broken = true; prop.respawn = 0;
                match.Tick(.02f, default, default);
                Assert.That(obstacle.Contains(prop.point, .65f), Is.False);
            }
        }

        [TestCase(ObstacleKind.Wall)]
        [TestCase(ObstacleKind.Pipe)]
        public void AiCanReachATargetBeyondSolidTerrain(ObstacleKind kind)
        {
            obstacle.kind = kind; obstacle.size = new Vector2(3, .6f);
            match.Tuning.aiGraceSeconds = 0;
            cars[0].position = new Vector2(0, 3);
            var ai = cars[2]; ai.computer = true;
            ai.Ai.BeginMatch(AiPersonality.Bumper);
            ai.position = new Vector2(0, -3); ai.heading = 90;
            cars[1].alive = cars[3].alive = false;
            float closest = 10;
            for (int frame = 0; frame < 60 * 12; frame++)
            {
                match.Tick(1f / 60, default, default);
                closest = Mathf.Min(closest, Vector2.Distance(ai.position, cars[0].position));
            }
            Assert.That(closest, Is.LessThan(1), "An obstacle must not pin a driver trying to reach the other side.");
        }

        [Test]
        public void AiCanLeaveTheTurningMarginAfterBeingPushedAgainstAWall()
        {
            obstacle.kind = ObstacleKind.Wall; obstacle.size = new Vector2(3, .4f);
            match.Tuning.aiGraceSeconds = 0;
            cars[0].position = new Vector2(0, 3);
            var ai = cars[2]; ai.computer = true; ai.Ai.BeginMatch(AiPersonality.Bumper);
            ai.position = new Vector2(0, -.522f); ai.heading = 90;
            cars[1].alive = cars[3].alive = false;
            float closest = 10;
            for (int i = 0; i < 60 * 12; i++)
            {
                match.Tick(1f / 60, default, default);
                closest = Mathf.Min(closest, Vector2.Distance(ai.position, cars[0].position));
            }
            Assert.That(closest, Is.LessThan(1));
        }

        [Test]
        public void MainSceneKeepsStartsCratesPropsAndAllRevivesClearOfEightFixedObstacles()
        {
            var loaded = EditorSceneManager.OpenPreviewScene("Assets/Scenes/GAME3_Mode7.unity");
            try
            {
                var game = loaded.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<DemolitionGame>()).Single();
                var tuning = new MatchTuning { arenaMin = game.arenaMin, arenaMax = game.arenaMax };
                var round = new DemolitionMatch(game.cars, game.props, game.pickups, game.rockets, tuning, game.obstacles);
                round.Reset(1);
                Assert.That(game.obstacles.Length, Is.EqualTo(8));
                foreach (var o in game.obstacles)
                {
                    Assert.That(game.cars.All(c => !o.Contains(c.position, .32f)), Is.True);
                    Assert.That(game.pickups.All(p => !o.Contains(p.point, .65f)), Is.True);
                    Assert.That(game.props.All(p => !o.Contains(p.point, .65f)), Is.True);
                }
                foreach (var car in game.cars)
                {
                    foreach (var c in game.cars) c.score = 100;
                    car.score = -1;
                    round.EliminateLowest();
                    Assert.That(round.Revive(car), Is.True);
                    Assert.That(game.obstacles.All(o => !o.Contains(car.position, .32f)), Is.True);
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(loaded); }
        }
    }
}
