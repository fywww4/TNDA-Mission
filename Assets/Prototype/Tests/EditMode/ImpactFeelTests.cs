using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TNDA.Prototype.Tests
{
    // Ticket 18: rams have weight. Faster hits knock cars further (up to a cap), side hits spin the victim, and
    // same-direction scrapes push both cars apart for less damage. Bare cars in a preview scene, as in DemolitionMatchTests.
    public class ImpactFeelTests
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
            match = new DemolitionMatch(cars, new BreakableProp[0], new ArenaPickup[0],
                new[] { new ArcadeProjectile(), new ArcadeProjectile() }, new MatchTuning { randomPropSpots = false });
            Random.InitState(18);
        }

        [TearDown]
        public void CloseScene() => EditorSceneManager.ClosePreviewScene(scene);

        // Humans with no input only coast; P1 at (0,-1) heading east at `speed`, car 3 placed by the caller.
        private CarRammed P1Hits(float speed, Vector2 victimAt, float victimHeading, float victimSpeed = 0)
        {
            match.Reset(1);
            foreach (var car in cars) { car.computer = false; car.spawnProtection = 0; car.position = new Vector2(-6 + car.index, 3); }
            cars[0].position = new Vector2(0, -1); cars[0].heading = 0; cars[0].speed = speed;
            cars[3].position = victimAt; cars[3].heading = victimHeading; cars[3].speed = victimSpeed;
            match.Tick(Dt, Idle, Idle);
            return match.Events.OfType<CarRammed>().SingleOrDefault();
        }

        private CarRammed HeadOn(float speed) => P1Hits(speed, new Vector2(.55f, -1), 180);

        [Test]
        public void AFasterHeadOnRamKnocksTheVictimFurtherInProportion()
        {
            HeadOn(1.5f);
            float slow = cars[3].SlideVelocity.magnitude - match.Tuning.contactBump;
            HeadOn(3f);
            float fast = cars[3].SlideVelocity.magnitude - match.Tuning.contactBump;

            Assert.That(slow, Is.GreaterThan(0));
            Assert.That(fast / slow, Is.EqualTo(2).Within(.05f));
        }

        [Test]
        public void TheKnockbackIsCapped()
        {
            HeadOn(8);
            float hard = cars[3].SlideVelocity.magnitude;
            HeadOn(14);
            float harder = cars[3].SlideVelocity.magnitude;

            Assert.That(harder, Is.EqualTo(hard).Within(.001f));
            Assert.That(hard, Is.EqualTo(match.Tuning.contactBump + match.Tuning.ramBumpMax).Within(.001f));
        }

        [Test]
        public void TheRammerReboundsHarderTheFasterItHit()
        {
            HeadOn(1.5f);
            float slow = cars[0].SlideVelocity.magnitude;
            HeadOn(3f);
            float fast = cars[0].SlideVelocity.magnitude;

            Assert.That(fast, Is.GreaterThan(slow + .2f));
        }

        // P1 drives into car 3's flank (car 3 faces north); returns how far car 3 turned once it settled.
        private float SideHitSpin(float speed, out CarRammed ram, out float stunRightAfter)
        {
            ram = P1Hits(speed, new Vector2(.55f, -1), 90);
            stunRightAfter = cars[3].stun;
            float turned = 0, last = cars[3].heading;
            for (int f = 0; f < 90; f++)
            {
                match.Tick(Dt, Idle, Idle);
                turned += Mathf.Abs(Mathf.DeltaAngle(last, cars[3].heading));
                last = cars[3].heading;
            }
            return turned;
        }

        [Test]
        public void ASideHitSpinsTheVictimHalfToAFullTurnWithALongerStun()
        {
            float turned = SideHitSpin(2.2f, out var ram, out float stun);

            Assert.AreEqual(ImpactKind.Side, ram.Kind);
            Assert.AreEqual(2, ram.Multiplier);
            Assert.That(turned, Is.InRange(180f, 360f));
            Assert.That(stun, Is.GreaterThan(.3f));
        }

        [Test]
        public void AFasterSideHitSpinsTheVictimFurther()
        {
            float slow = SideHitSpin(1.5f, out _, out _);
            float fast = SideHitSpin(3.4f, out _, out _);

            Assert.That(fast, Is.GreaterThan(slow + 60));
            Assert.That(fast, Is.LessThanOrEqualTo(360.5f));
        }

        [Test]
        public void HeadOnAndRearRamsKeepTheirKindsAndDoNotSpin()
        {
            var headOn = HeadOn(2.8f);
            Assert.AreEqual(ImpactKind.Front, headOn.Kind);
            Assert.AreEqual(1, headOn.Multiplier);
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(180, cars[3].heading)), Is.LessThanOrEqualTo(30));

            var rear = P1Hits(2.8f, new Vector2(.55f, -1), 0);
            Assert.AreEqual(ImpactKind.Rear, rear.Kind);
            Assert.AreEqual(1.5f, rear.Multiplier);
        }

        // Car 3 runs alongside P1, just north of it and nosing 8° towards it.
        private CarRammed Scrape(float p1Speed, float otherSpeed) => P1Hits(p1Speed, new Vector2(.1f, -.45f), 8, otherSpeed);

        [Test]
        public void CarsSideBySideScrapeApartAndBothSlowDown()
        {
            var ram = Scrape(3, 2);

            Assert.IsNotNull(ram);
            Assert.AreEqual(ImpactKind.Scrape, ram.Kind);
            Assert.AreSame(cars[0], ram.Attacker);
            Assert.That(cars[0].SlideVelocity.y, Is.LessThan(-match.Tuning.contactBump * .5f));
            Assert.That(cars[3].SlideVelocity.y, Is.GreaterThan(match.Tuning.contactBump * .5f));
            Assert.That(cars[0].speed, Is.LessThan(2.8f));
            Assert.That(cars[3].speed, Is.LessThan(1.85f));
            Assert.That(cars[3].stun, Is.EqualTo(0));
        }

        [Test]
        public void AScrapeHurtsAndScoresLessThanAHeadOnRamAtTheSameSpeed()
        {
            HeadOn(3);
            float headOnDamage = 1 - cars[3].health, headOnPoints = cars[0].score;
            Scrape(3, 2);
            float scrapeDamage = 1 - cars[3].health, scrapePoints = cars[0].score;

            Assert.That(scrapeDamage, Is.GreaterThan(0));
            Assert.That(scrapeDamage, Is.LessThan(headOnDamage * .6f));
            Assert.That(scrapePoints, Is.GreaterThan(0));
            Assert.That(scrapePoints, Is.LessThan(headOnPoints * .6f));
        }

        [Test]
        public void CruisingSideBySideAtTheSameSpeedScrapesWithoutHarm()
        {
            var ram = P1Hits(3, new Vector2(.1f, -.45f), 0, 3);

            Assert.AreEqual(ImpactKind.Scrape, ram.Kind);
            Assert.AreEqual(1, cars[3].health);
            Assert.AreEqual(0, cars[0].score);
        }

        [Test]
        public void AShieldedCarHitSideOnDoesNotSpin()
        {
            match.Reset(1);
            foreach (var car in cars) { car.computer = false; car.spawnProtection = 0; car.position = new Vector2(-6 + car.index, 3); }
            cars[0].position = new Vector2(0, -1); cars[0].heading = 0; cars[0].speed = 3;
            cars[3].position = new Vector2(.55f, -1); cars[3].heading = 90; cars[3].shieldSeconds = 5;
            match.Tick(Dt, Idle, Idle);

            Assert.That(cars[3].stun, Is.LessThan(.2f));
        }

        [Test]
        public void CreepingAlongsideIsOnlyATouch()
        {
            Assert.IsNull(Scrape(.5f, .4f));
        }
    }
}
