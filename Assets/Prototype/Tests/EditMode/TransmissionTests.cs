using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TNDA.Prototype.Tests
{
    // Public driving boundary agreed with the user for ticket 14.
    public class TransmissionTests
    {
        private ArcadeVehicle car;
        private Scene scene;
        private const float Dt = 1f / 60;
        private static readonly ArcadeInput Gas = new ArcadeInput { throttle = 1 };

        [SetUp]
        public void CreateCar()
        {
            scene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Transmission test");
            SceneManager.MoveGameObjectToScene(root, scene);
            car = root.AddComponent<ArcadeVehicle>();
            car.ResetVehicle(Vector2.zero, 0, 1);
        }

        [TearDown]
        public void DestroyCar() => EditorSceneManager.ClosePreviewScene(scene);

        private void SelectGear(int gear)
        {
            while (car.Gear < gear) {
                int before = car.Gear;
                car.Drive(Dt, new ArcadeInput { shiftUpPressed = true }, 2.8f, 1.4f, manual: true);
                Assert.That(car.Gear, Is.GreaterThan(before), "upshift");
            }
            while (car.Gear > gear) {
                int before = car.Gear;
                car.Drive(Dt, new ArcadeInput { shiftDownPressed = true }, 2.8f, 1.4f, manual: true);
                Assert.That(car.Gear, Is.LessThan(before), "downshift");
            }
        }

        [TestCase(0, -1f)]
        [TestCase(1, 1.2f)]
        [TestCase(2, 1.9f)]
        [TestCase(3, 2.5f)]
        [TestCase(4, 3f)]
        public void ThrottleReachesTheSelectedGearsSignedSpeedLimit(int gear, float limit)
        {
            SelectGear(gear);
            for (int i = 0; i < 600; i++) car.Drive(Dt, Gas, 2.8f, 1.4f, manual: true);
            Assert.That(car.speed, Is.EqualTo(limit).Within(.001f));
            Assert.That(car.position.x * limit, Is.GreaterThan(0), "reverse actually moves backwards");
        }

        [TestCase(1, .525f)]
        [TestCase(2, .42f)]
        [TestCase(3, .35f)]
        [TestCase(4, .2275f)]
        public void HigherGearsPullAwayMoreSlowlyWithoutStallingOrTakingDamage(int gear, float speed)
        {
            SelectGear(gear);
            car.Drive(.25f, Gas, 2.8f, 1.4f, manual: true);
            Assert.That(car.speed, Is.EqualTo(speed).Within(.001f));
            Assert.That(car.alive && car.health == 1, Is.True);
        }

        [Test]
        public void AutomaticKeepsTheOldRampAndCoastingWhileShiftingOnlyThroughThreeGears()
        {
            Assert.That(car.Gear, Is.EqualTo(1));
            car.Drive(.5f, Gas, 2.8f, 1.4f);
            Assert.That(car.speed, Is.EqualTo(.7f).Within(.001f));
            car.Drive(.5f, Gas, 2.8f, 1.4f);
            Assert.That(car.Gear, Is.EqualTo(2));
            car.Drive(.5f, Gas, 2.8f, 1.4f);
            Assert.That(car.Gear, Is.EqualTo(3));
            car.Drive(5, new ArcadeInput { throttle = 1, shiftUpPressed = true }, 2.8f, 1.4f);
            Assert.That(car.speed, Is.EqualTo(2.8f));
            Assert.That(car.Gear, Is.EqualTo(3));
            car.Drive(.5f, new ArcadeInput(), 2.8f, 2.2f);
            Assert.That(car.speed, Is.EqualTo(1.7f).Within(.001f));
            Assert.That(car.Gear, Is.EqualTo(2));
            car.Drive(1, new ArcadeInput(), 2.8f, 2.2f);
            Assert.That(car.speed, Is.Zero);
            Assert.That(car.Gear, Is.EqualTo(1));
        }

        [Test]
        public void EveryNewLifeStartsInFirstGearIncludingAPaidRejoin()
        {
            SelectGear(4);
            car.ResetVehicle(Vector2.zero, 0, 1);
            Assert.That(car.Gear, Is.EqualTo(1), "new match");
            SelectGear(3);
            car.spawnProtection = 0;
            var rules = new VehicleLifecycleRules { respawnSeconds = 3, spinStartSpeed = 4 };
            car.TakeDamage(2, rules);
            car.Respawn(Vector2.zero, 0);
            Assert.That(car.Gear, Is.EqualTo(1), "wreck respawn");
            SelectGear(0);
            car.Eliminate(4, rules);
            car.Rejoin(100);
            car.Respawn(Vector2.zero, 0);
            Assert.That(car.Gear, Is.EqualTo(1), "paid continue");
        }
    }
}
