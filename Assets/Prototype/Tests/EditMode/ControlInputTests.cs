using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TNDA.Prototype.Tests
{
    public class ControlInputTests : InputTestFixture
    {
        [Test]
        public void TheOldTypeBKeysDoNothing()
        {
            // The old Type B layout (and its saved choice) is gone: up/down arrows and left Ctrl are not controls.
            PlayerPrefs.SetInt("TNDA.ControlType", 1);
            try
            {
                var keyboard = InputSystem.AddDevice<Keyboard>();
                Press(keyboard.upArrowKey);
                Press(keyboard.downArrowKey);
                Press(keyboard.leftCtrlKey);
                var p1 = ArcadeGame.ReadInput(0);
                Assert.That(p1.throttle == 0 && !p1.brake && !p1.action, Is.True);
                Press(keyboard.zKey);
                Assert.That(ArcadeGame.ReadInput(0).throttle, Is.EqualTo(1));
            }
            finally { PlayerPrefs.DeleteKey("TNDA.ControlType"); }
        }

        [Test]
        public void KeyboardLayoutMapsGasItemAndBrakeWithoutChangingP2()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            Press(keyboard.zKey);
            Press(keyboard.xKey);
            Press(keyboard.aKey);
            Press(keyboard.leftArrowKey);
            var p1 = ArcadeGame.ReadInput(0);
            Assert.That(p1.throttle, Is.EqualTo(1));
            Assert.That(p1.action && p1.brake, Is.True);
            Assert.That(p1.steer, Is.EqualTo(-1));
            Release(keyboard.xKey);
            Press(keyboard.xKey);
            Assert.That(ArcadeGame.ReadInput(0).actionPressed, Is.True);
            InputSystem.Update();
            Assert.That(ArcadeGame.ReadInput(0).actionPressed, Is.False, "holding an item key is not a new press");
            var p2 = ArcadeGame.ReadInput(1);
            Assert.That(p2.throttle == 0 && !p2.action && !p2.brake, Is.True);
            Press(keyboard.numpad8Key);
            Press(keyboard.numpadEnterKey);
            Press(keyboard.numpad2Key);
            p2 = ArcadeGame.ReadInput(1);
            Assert.That(p2.throttle, Is.EqualTo(1));
            Assert.That(p2.action && p2.brake, Is.True);
        }

        [TestCase(0)]
        [TestCase(1)]
        public void EachPadUsesTheSfcLayoutAndOnlyControlsItsOwnSeat(int seat)
        {
            var first = InputSystem.AddDevice<Gamepad>();
            var second = InputSystem.AddDevice<Gamepad>();
            var pad = seat == 0 ? first : second;
            Press(pad.buttonSouth);
            Set(pad.leftStick, new Vector2(.5f, 1));
            Press(pad.buttonEast);
            var input = ArcadeGame.ReadInput(seat);
            Assert.That(input.throttle, Is.EqualTo(1));
            Assert.That(input.action, Is.True);
            Release(pad.buttonEast);
            Press(pad.buttonEast);
            Assert.That(ArcadeGame.ReadInput(seat).actionPressed, Is.True);
            Assert.That(input.steer, Is.GreaterThan(0));
            Assert.That(ArcadeGame.ReadInput(1 - seat).throttle, Is.Zero);
            Assert.That(ArcadeGame.ReadInput(1 - seat).action, Is.False);
            Press(pad.buttonWest);
            Assert.That(ArcadeGame.ReadInput(seat).brake, Is.True);
            Release(pad.buttonSouth);
            Assert.That(ArcadeGame.ReadInput(seat).throttle, Is.Zero, "stick up never accelerates");
        }

        [TestCase(Key.Enter)]
        [TestCase(Key.Space)]
        public void P1StartAcceptsEnterAndSpace(Key start)
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            Press(keyboard[start]);
            Assert.That(ArcadeGame.ReadInput(0).startPressed, Is.True);
            Assert.That(ArcadeGame.ReadInput(1).startPressed, Is.False);
        }
    }
}
