using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TNDA.Prototype.Tests
{
    public class BrakingTests
    {
        private ArcadeVehicle car;
        private Scene scene;
        private const float Dt = 1f / 60;

        [SetUp]
        public void CreateCar()
        {
            scene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Brake test");
            SceneManager.MoveGameObjectToScene(root, scene);
            car = root.AddComponent<ArcadeVehicle>();
            car.ResetVehicle(Vector2.zero, 0, 1);
        }

        [TearDown]
        public void DestroyCar() => EditorSceneManager.ClosePreviewScene(scene);

        [TestCase(false)]
        [TestCase(true)]
        public void BrakingOverridesGasAndSlowsMuchFasterThanCoasting(bool manual)
        {
            car.speed = 2.8f;
            car.Drive(.2f, default, 2.8f, 2.2f, manual);
            float coast = car.speed;
            car.speed = 2.8f;
            car.Drive(.2f, new ArcadeInput { brake = true, throttle = 1 }, 2.8f, 1.4f, manual);
            Assert.That(car.speed, Is.GreaterThanOrEqualTo(0));
            Assert.That(2.8f - car.speed, Is.GreaterThan((2.8f - coast) * 2));
        }

        [Test]
        public void AutomaticStopsBeforeReversingAndSharesTheManualReverseLimit()
        {
            car.speed = 2.8f;
            var brake = new ArcadeInput { brake = true };
            for (int i = 0; i < 60 && car.speed > 0; i++) car.Drive(Dt, brake, 2.8f, 2.2f);
            Assert.That(car.speed, Is.Zero, "must reach a stop before going backwards");
            float stoppedAt = car.position.x;
            for (int i = 0; i < 180; i++) car.Drive(Dt, brake, 2.8f, 2.2f);
            Assert.That(car.speed, Is.EqualTo(-1).Within(.001f));
            Assert.That(car.Gear, Is.Zero);
            Assert.That(car.position.x, Is.LessThan(stoppedAt));
            car.Drive(1, default, 2.8f, 2.2f);
            Assert.That(car.speed, Is.Zero, "releasing reverse coasts to a stop");
            car.Drive(.5f, new ArcadeInput { throttle = 1 }, 2.8f, 1.4f);
            Assert.That(car.speed, Is.GreaterThan(0), "gas drives forwards again");
        }

        [Test]
        public void ManualBrakeNeverSelectsReverseAndAlsoStopsReverseGear()
        {
            for (int i = 0; i < 120; i++) car.Drive(Dt, new ArcadeInput { brake = true }, 2.8f, 2.2f, manual: true);
            Assert.That(car.speed, Is.Zero);
            Assert.That(car.Gear, Is.EqualTo(1));
            car.Drive(Dt, new ArcadeInput { shiftDownPressed = true }, 2.8f, 1.4f, manual: true);
            for (int i = 0; i < 120; i++) car.Drive(Dt, new ArcadeInput { throttle = 1 }, 2.8f, 1.4f, manual: true);
            Assert.That(car.speed, Is.EqualTo(-1).Within(.001f));
            for (int i = 0; i < 120; i++) car.Drive(Dt, new ArcadeInput { brake = true }, 2.8f, 2.2f, manual: true);
            Assert.That(car.speed, Is.Zero);
            Assert.That(car.Gear, Is.Zero, "R stays selected");
        }
    }
}
