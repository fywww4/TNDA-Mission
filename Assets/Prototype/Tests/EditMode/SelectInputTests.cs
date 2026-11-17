using NUnit.Framework;
using UnityEngine.InputSystem;

namespace TNDA.Prototype.Tests
{
    // InputTestFixture isolates devices and restores the user's input system after each test.
    public class SelectInputTests : InputTestFixture
    {
        [Test]
        public void TabAndNumpadZeroHoldSelectForTheirOwnSeat()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            Press(keyboard.tabKey);
            Assert.That(ArcadeGame.ReadInput(0).selectHeld, Is.True);
            Assert.That(ArcadeGame.ReadInput(1).selectHeld, Is.False);
            Release(keyboard.tabKey);
            Assert.That(ArcadeGame.ReadInput(0).selectHeld, Is.False);
            Press(keyboard.numpad0Key);
            Assert.That(ArcadeGame.ReadInput(0).selectHeld, Is.False);
            Assert.That(ArcadeGame.ReadInput(1).selectHeld, Is.True);
            Release(keyboard.numpad0Key);
            Assert.That(ArcadeGame.ReadInput(1).selectHeld, Is.False);
        }

        [Test]
        public void EachGamepadsBackButtonHoldsOnlyItsSeatsSelect()
        {
            var first = InputSystem.AddDevice<Gamepad>();
            var second = InputSystem.AddDevice<Gamepad>();
            Press(second.selectButton);
            Assert.That(ArcadeGame.ReadInput(0).selectHeld, Is.False);
            Assert.That(ArcadeGame.ReadInput(1).selectHeld, Is.True);
            Press(first.selectButton);
            Assert.That(ArcadeGame.ReadInput(0).selectHeld, Is.True);
            Release(second.selectButton);
            Assert.That(ArcadeGame.ReadInput(1).selectHeld, Is.False);
            Assert.That(ArcadeGame.ReadInput(0).selectHeld, Is.True);
            Release(first.selectButton);
            Assert.That(ArcadeGame.ReadInput(0).selectHeld, Is.False);
        }
    }
}
