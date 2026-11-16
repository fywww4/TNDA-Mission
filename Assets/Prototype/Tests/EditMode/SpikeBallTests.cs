using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TNDA.Prototype.Tests
{
    // Ticket 21: rolling spike balls. A car that drives into one takes part damage and knocks it flying; a flying ball
    // wrecks the next car it reaches and the knocker gets the wreck bonus.
    public class SpikeBallTests
    {
        private Scene scene;
        private ArcadeVehicle[] cars;
        private DemolitionMatch match;
        private SpikeBall ball;
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
            ball = new SpikeBall { point = Vector2.zero, heading = 0 };
            match = new DemolitionMatch(cars, new BreakableProp[0], new ArenaPickup[0], new[] { new ArcadeProjectile() },
                new MatchTuning { randomPropSpots = false, arenaMin = new Vector2(-12, -10), arenaMax = new Vector2(12, 10) },
                spikeBalls: new[] { ball });
            match.Reset(2);
            match.Tuning.accelerationPerSecond = 0; // keep each car's speed exact at contact
            // Parked out of the way, every car hittable from the start.
            foreach (var c in cars) { c.computer = c.index >= 2; c.position = new Vector2(-10 + c.index * 2, -8); c.spawnProtection = 0; c.speed = 0; }
            match.Tuning.aiGraceSeconds = 0;
        }

        [TearDown]
        public void TearDown() => EditorSceneManager.ClosePreviewScene(scene);

        // Car 0 drives up (heading 90) into the ball from just below it.
        private void DriveIntoBall(float speed, int car = 0)
        {
            // One tick apart first, so this is a fresh touch.
            cars[car].position = new Vector2(5, 5); cars[car].speed = 0;
            match.Tick(.02f, default, default);
            ball.position = Vector2.zero; ball.velocity = Vector2.zero;
            cars[car].position = new Vector2(0, -.61f); cars[car].heading = 90; cars[car].speed = speed; cars[car].stun = 0;
            match.Tick(.02f, car == 0 ? Gas : default, car == 1 ? Gas : default);
        }

        [Test]
        public void DrivingIntoABallCostsAQuarterOfDurabilityAndKnocksItAwayByImpactSpeed()
        {
            DriveIntoBall(1.5f);
            Assert.That(cars[0].health, Is.EqualTo(.75f).Within(.001f));
            float slow = ball.velocity.magnitude;
            Assert.That(ball.velocity.y, Is.GreaterThan(0), "The ball flies off the way the car pushed it.");
            Assert.That(ball.Flying, Is.True);
            Assert.That(ball.Launcher, Is.SameAs(cars[0]));

            cars[0].health = 1;
            DriveIntoBall(3f);
            Assert.That(ball.velocity.magnitude, Is.GreaterThan(slow * 1.5f), "A faster hit sends the ball faster.");
            Assert.That(match.Events.OfType<SpikeBallHit>().Count(), Is.EqualTo(2));
        }

        // Ticks until the ball has reached a car (or a second passes).
        private SpikeBallStruck TickUntilStruck()
        {
            for (int i = 0; i < 50; i++)
            {
                match.Tick(.02f, default, default);
                var struck = match.Events.OfType<SpikeBallStruck>().FirstOrDefault();
                if (struck != null) return struck;
            }
            return null;
        }

        [Test]
        public void AKnockedBallWrecksTheNextCarAndTheKnockerGetsTheWreckBonus()
        {
            cars[2].position = new Vector2(0, 2.2f); cars[2].score = 500;
            DriveIntoBall(3f);
            cars[0].position = new Vector2(-5, -5); cars[0].speed = 0; // out of the ball's way
            var struck = TickUntilStruck();
            Assert.That(struck, Is.Not.Null, "The flying ball reaches the car in its path.");
            Assert.That(struck.Launcher, Is.SameAs(cars[0]));
            Assert.That(struck.Wrecked, Is.True);
            Assert.That(cars[2].alive, Is.False, "A flying ball wrecks outright, like a cherry.");
            Assert.That(cars[2].score, Is.EqualTo(250), "The wrecked car loses the usual 250.");
            Assert.That(cars[0].score, Is.EqualTo(300), "The car that knocked the ball gets the 300 wreck bonus.");
            Assert.That(match.Events.OfType<VehicleWrecked>().Any(w => w.Car == cars[2]), Is.True, "So the wreck explosion plays.");
            Assert.That(ball.Flying, Is.False, "After hitting a car the ball is back to rolling.");
        }

        [Test]
        public void AComputerCarHittingABallFollowsTheSameRules()
        {
            DriveIntoBall(3f, car: 2);
            Assert.That(cars[2].health, Is.EqualTo(.75f).Within(.001f));
            Assert.That(ball.Launcher, Is.SameAs(cars[2]));
        }

        [Test]
        public void ABumperAfterACarDrivesThroughASpikeBallLinedUpWithIt()
        {
            Assume.That(cars[3].Ai.Personality, Is.EqualTo(AiPersonality.Bumper));
            cars[3].position = new Vector2(0, -3); cars[3].heading = 90;
            cars[1].position = new Vector2(0, 1.5f); // its nearest quarry, straight ahead past the ball
            ball.position = new Vector2(0, -1.5f); ball.velocity = Vector2.zero;
            match.Tuning.spikeRollSpeed = 0; // keep the ball in place
            for (int i = 0; i < 10 && cars[3].Ai.Target == null; i++) match.Tick(.1f, default, default);
            Assert.That(cars[3].Ai.Target, Is.SameAs(cars[1]));
            Assert.That(cars[3].Ai.Destination.x, Is.EqualTo(0).Within(.05f));
            Assert.That(cars[3].Ai.Destination.y, Is.InRange(-1.5f, -1f), "It aims through the ball, not straight at the car.");
        }

        [Test]
        public void ARollingBallOnlyHurtsTheCarItRollsInto()
        {
            cars[2].position = new Vector2(.62f, 0); cars[2].heading = 90;
            ball.position = Vector2.zero; ball.velocity = new Vector2(match.Tuning.spikeRollSpeed, 0);
            for (int i = 0; i < 10; i++) match.Tick(.02f, default, default);
            Assert.That(match.Events.OfType<SpikeBallStruck>(), Is.Empty);
            Assert.That(match.Events.OfType<SpikeBallHit>().Single().Car, Is.SameAs(cars[2]));
            Assert.That(cars[2].alive, Is.True);
            Assert.That(cars[2].health, Is.EqualTo(.75f).Within(.001f));
            Assert.That(ball.Flying, Is.False, "A slow touch does not knock the ball flying.");
        }

        [Test]
        public void AShieldOrSpawnProtectionStopsAFlyingBall()
        {
            cars[2].position = new Vector2(-.3f, 2.2f); cars[2].shieldSeconds = 5;
            cars[3].position = new Vector2(.3f, 4.2f); cars[3].spawnProtection = 5;
            DriveIntoBall(3f);
            cars[0].position = new Vector2(-5, -5); cars[0].speed = 0;
            var struck = TickUntilStruck();
            Assert.That(struck.Victim, Is.SameAs(cars[2]));
            Assert.That(struck.Wrecked, Is.False);
            Assert.That(cars[2].alive && cars[2].health == 1, Is.True);
            Assert.That(match.Events.OfType<ShieldBlocked>().Any(b => b.Car == cars[2]), Is.True);
            Assert.That(cars[0].score, Is.Zero);

            match.ClearEvents();
            ball.position = new Vector2(.3f, 3); ball.velocity = new Vector2(0, 5); ball.Launcher = cars[0];
            struck = TickUntilStruck();
            Assert.That(struck.Victim, Is.SameAs(cars[3]));
            Assert.That(cars[3].alive && cars[3].health == 1, Is.True, "Spawn protection blocks it too.");
        }

        [Test]
        public void AFlyingBallSlowsBackToARollAndBouncesOffTheArenaEdge()
        {
            DriveIntoBall(3f);
            cars[0].position = new Vector2(-5, -5); cars[0].speed = 0;
            for (int i = 0; i < 200; i++) match.Tick(.02f, default, default);
            Assert.That(ball.Flying, Is.False);
            Assert.That(ball.velocity.magnitude, Is.EqualTo(match.Tuning.spikeRollSpeed).Within(.001f));

            ball.position = new Vector2(0, 9.6f); ball.velocity = new Vector2(0, .6f);
            for (int i = 0; i < 10; i++) match.Tick(.1f, default, default);
            Assert.That(ball.velocity.y, Is.LessThan(0), "It rolls back off the edge.");
            Assert.That(ball.position.y, Is.LessThan(match.Tuning.arenaMax.y));
        }

        [Test]
        public void ABallBouncesOffSolidTerrain()
        {
            var wall = new ArenaObstacle { kind = ObstacleKind.Wall, point = new Vector2(0, 1), size = new Vector2(3, .4f) };
            match = new DemolitionMatch(cars, new BreakableProp[0], new ArenaPickup[0], new[] { new ArcadeProjectile() }, match.Tuning,
                new[] { wall }, new[] { ball });
            match.Reset(2);
            foreach (var c in cars) c.position = new Vector2(-10 + c.index * 2, -8);
            ball.position = Vector2.zero; ball.velocity = new Vector2(0, 3);
            for (int i = 0; i < 20; i++) match.Tick(.02f, default, default);
            Assert.That(ball.velocity.y, Is.LessThan(0));
            Assert.That(ball.position.y, Is.LessThan(1 - .2f));
        }

        [Test]
        public void ACarPushingABallAgainstAWallNeverPutsItInsideTheWall()
        {
            var wall = new ArenaObstacle { kind = ObstacleKind.Wall, point = new Vector2(0, .5f), size = new Vector2(3, .4f) };
            match = new DemolitionMatch(cars, new BreakableProp[0], new ArenaPickup[0], new[] { new ArcadeProjectile() }, match.Tuning,
                new[] { wall }, new[] { ball });
            match.Reset(2);
            foreach (var c in cars) c.position = new Vector2(-10 + c.index * 2, -8);
            // The ball sits against the wall's face; the car drives straight up into it.
            ball.position = new Vector2(0, .3f - match.Tuning.spikeRadius - .01f); ball.velocity = Vector2.zero;
            cars[0].position = new Vector2(0, ball.position.y - .58f); cars[0].heading = 90; cars[0].speed = 3; cars[0].stun = 0;
            match.Tick(.02f, Gas, default);
            Assert.That(match.Events.OfType<SpikeBallHit>().Any(), Is.True);
            Assert.That(wall.Contains(ball.position, match.Tuning.spikeRadius - .01f), Is.False);
            for (int i = 0; i < 30; i++) match.Tick(.02f, default, default);
            Assert.That(ball.position.y, Is.LessThan(.3f), "It bounces back instead of tunnelling through.");
        }

        [Test]
        public void RandomSpotsKeepBallsAwayFromStartsCratesAndTerrain()
        {
            var wall = new ArenaObstacle { kind = ObstacleKind.Wall, point = new Vector2(0, 0), size = new Vector2(4, 4) };
            var crate = new ArenaPickup { point = new Vector2(5, 0) };
            var balls = new[] { new SpikeBall(), new SpikeBall() };
            match.Tuning.randomPropSpots = true;
            match = new DemolitionMatch(cars, new BreakableProp[0], new[] { crate }, new[] { new ArcadeProjectile() }, match.Tuning,
                new[] { wall }, balls);
            for (int round = 0; round < 20; round++)
            {
                match.Reset(2);
                var starts = cars.Select(c => c.position).ToArray();
                foreach (var b in balls)
                {
                    Assert.That(wall.Contains(b.position, .5f), Is.False);
                    Assert.That(Vector2.Distance(b.position, crate.point), Is.GreaterThan(1));
                    Assert.That(starts.Min(s => Vector2.Distance(s, b.position)), Is.GreaterThan(1.5f));
                    Assert.That(b.velocity.magnitude, Is.EqualTo(match.Tuning.spikeRollSpeed).Within(.001f));
                }
                Assert.That(Vector2.Distance(balls[0].position, balls[1].position), Is.GreaterThan(2));
            }
        }

        [Test]
        public void ASpikeBallShakesOnlyThePlayerItHits()
        {
            var feel = new ImpactFeelTuning();
            var wreck = ImpactFeel.Kicks(new SpikeBallStruck(cars[0], cars[1], true, Vector2.zero), 2.1f, feel).Single();
            Assert.That(wreck.Car, Is.SameAs(cars[1]));
            Assert.That(wreck.Pixels, Is.EqualTo(feel.wreckPixels));
            var hit = ImpactFeel.Kicks(new SpikeBallHit(cars[0], 2, true, false, Vector2.zero), 2.1f, feel).Single();
            Assert.That(hit.Car, Is.SameAs(cars[0]));
            Assert.That(hit.Pixels, Is.EqualTo(feel.smashPixels));
            Assert.That(ImpactFeel.Kicks(new SpikeBallStruck(cars[0], cars[2], true, Vector2.zero), 2.1f, feel), Is.Empty,
                "A computer car being hit shakes nobody.");
        }
    }
}
