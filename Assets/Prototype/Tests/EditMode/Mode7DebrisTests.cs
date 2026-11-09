using NUnit.Framework;
using UnityEngine;

namespace TNDA.Prototype.Tests
{
    public class Mode7DebrisTests
    {
        private static readonly Color Cyan = new Color(0, 1, 1);

        private static void AssertColour(Color expected, Color32 actual)
        {
            Assert.That(actual.r / 255f, Is.EqualTo(expected.r).Within(.01f), "r");
            Assert.That(actual.g / 255f, Is.EqualTo(expected.g).Within(.01f), "g");
            Assert.That(actual.b / 255f, Is.EqualTo(expected.b).Within(.01f), "b");
        }

        [Test]
        public void TheBodyRedsBecomeShadesOfTheSeatColour()
        {
            AssertColour(new Color(.45f, 1, 1), Mode7Debris.SeatRecolour(new Color32(0xF8, 0x78, 0x58, 255), Cyan));
            AssertColour(Cyan, Mode7Debris.SeatRecolour(new Color32(0xE0, 0x40, 0x20, 255), Cyan));
            AssertColour(new Color(0, .68f, .68f), Mode7Debris.SeatRecolour(new Color32(0xB0, 0x18, 0x00, 255), Cyan));
            AssertColour(new Color(0, .38f, .38f), Mode7Debris.SeatRecolour(new Color32(0x60, 0x00, 0x00, 255), Cyan));
        }

        [Test]
        public void OtherColoursAndTransparencyAreKept()
        {
            var glass = new Color32(0xA8, 0xD8, 0xE8, 255);
            Assert.AreEqual(glass, Mode7Debris.SeatRecolour(glass, Cyan));
            var clear = new Color32(0, 0, 0, 0);
            Assert.AreEqual(clear, Mode7Debris.SeatRecolour(clear, Cyan));
            Assert.AreEqual(128, Mode7Debris.SeatRecolour(new Color32(0xE0, 0x40, 0x20, 128), Cyan).a);
        }
    }
}
