using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace TNDA.Prototype.Tests
{
    // Ticket 18: which players' screens shake for a match event, how hard and how long, and when sparks fly.
    public class ImpactFeelPresentationTests
    {
        private readonly ImpactFeelTuning tuning = new ImpactFeelTuning();
        // The match's prop-smashing speed (MatchTuning.smashSpeed).
        private const float Smash = 2.1f;
        private ArcadeVehicle p1, p2, ai1, ai2;

        [SetUp]
        public void MakeCars()
        {
            p1 = Car(0, false); p2 = Car(1, false); ai1 = Car(2, true); ai2 = Car(3, true);
        }

        [TearDown]
        public void DropCars()
        {
            foreach (var car in new[] { p1, p2, ai1, ai2 }) Object.DestroyImmediate(car.gameObject);
        }

        private static ArcadeVehicle Car(int index, bool computer)
        {
            var car = new GameObject("Car " + index).AddComponent<ArcadeVehicle>();
            car.index = index; car.computer = computer;
            return car;
        }

        private static CarRammed Ram(ArcadeVehicle attacker, ArcadeVehicle victim, float impact,
            ImpactKind kind = ImpactKind.Front, bool wrecked = false) =>
            new CarRammed(attacker, victim, impact, 1, !wrecked, kind, wrecked);

        [Test]
        public void ComputerCarsRammingEachOtherShakeNoScreen()
        {
            Assert.IsEmpty(ImpactFeel.Kicks(Ram(ai1, ai2, 3.4f, wrecked: true), Smash, tuning));
        }

        [Test]
        public void ALightTouchShakesNothing()
        {
            Assert.IsEmpty(ImpactFeel.Kicks(Ram(p1, ai1, 1.2f), Smash, tuning));
        }

        [Test]
        public void OnlyThePlayersInTheRamShake()
        {
            var rammer = ImpactFeel.Kicks(Ram(p1, ai1, 2.8f), Smash, tuning).ToArray();
            Assert.AreEqual(1, rammer.Length);
            Assert.AreSame(p1, rammer[0].Car);

            var rammed = ImpactFeel.Kicks(Ram(ai1, p2, 2.8f), Smash, tuning).ToArray();
            Assert.AreEqual(1, rammed.Length);
            Assert.AreSame(p2, rammed[0].Car);

            var both = ImpactFeel.Kicks(Ram(p1, p2, 2.8f), Smash, tuning).Select(k => k.Car).ToArray();
            CollectionAssert.AreEquivalent(new[] { p1, p2 }, both);
        }

        [Test]
        public void HarderHitsShakeHarderAndLongerAndAWreckShakesHardest()
        {
            var light = ImpactFeel.Kicks(Ram(p1, ai1, 1.7f), Smash, tuning).Single();
            var smash = ImpactFeel.Kicks(Ram(p1, ai1, 2.4f), Smash, tuning).Single();
            var wreck = ImpactFeel.Kicks(Ram(p1, ai1, 2.4f, wrecked: true), Smash, tuning).Single();

            Assert.That(light.Pixels, Is.EqualTo(1));
            Assert.That(smash.Pixels, Is.GreaterThan(light.Pixels));
            Assert.That(wreck.Pixels, Is.GreaterThan(smash.Pixels));
            Assert.That(light.Seconds, Is.EqualTo(.15f).Within(.001f));
            Assert.That(smash.Seconds, Is.InRange(light.Seconds + .01f, wreck.Seconds - .01f));
            Assert.That(wreck.Seconds, Is.EqualTo(.35f).Within(.001f));
        }

        [Test]
        public void APlayerWreckedByARamGetsTheHardestShakeButOtherWrecksDoNotShake()
        {
            var kick = ImpactFeel.Kicks(Ram(ai1, p2, 2.4f, wrecked: true), Smash, tuning).Single();
            Assert.AreSame(p2, kick.Car);
            Assert.AreEqual(tuning.wreckPixels, kick.Pixels);

            Assert.IsEmpty(ImpactFeel.Kicks(new VehicleWrecked(p2, 250), Smash, tuning));
        }

        [Test]
        public void SparksFlyFromSmashSpeedRamsAndEveryScrape()
        {
            Assert.IsFalse(ImpactFeel.TrySparks(Ram(ai1, ai2, 1.8f), Smash, out _));
            Assert.IsTrue(ImpactFeel.TrySparks(Ram(ai1, ai2, 2.1f), Smash, out var point));
            Assert.AreEqual(Vector2.zero, point);
            Assert.IsTrue(ImpactFeel.TrySparks(Ram(ai1, ai2, 1.2f, ImpactKind.Scrape), Smash, out _));
            Assert.IsFalse(ImpactFeel.TrySparks(new VehicleWrecked(p1, 0), Smash, out _));
        }

        [Test]
        public void AShakeMovesTheViewByWholeArtPixelsAndSettles()
        {
            var shake = new ScreenShake();
            shake.Kick(3, .3f);
            bool moved = false;
            for (float t = 0; t < .28f; t += 1f / 60)
            {
                var offset = shake.Advance(1f / 60);
                Assert.That(Mathf.Abs(offset.x), Is.LessThanOrEqualTo(3));
                Assert.That(Mathf.Abs(offset.y), Is.LessThanOrEqualTo(3));
                moved |= offset != Vector2Int.zero;
            }
            Assert.IsTrue(moved);
            for (int f = 0; f < 4; f++) shake.Advance(1f / 60);
            Assert.AreEqual(Vector2Int.zero, shake.Advance(1f / 60));
        }

        [Test]
        public void AWeakerKickDoesNotCutAStrongShakeShort()
        {
            var shake = new ScreenShake();
            shake.Kick(3, .35f);
            shake.Kick(1, .15f);
            for (int f = 0; f < 12; f++) shake.Advance(1f / 60);
            Assert.That(shake.Pixels, Is.GreaterThanOrEqualTo(2));
        }
    }
}
