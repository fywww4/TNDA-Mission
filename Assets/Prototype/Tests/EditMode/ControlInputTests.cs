using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TNDA.Prototype.Tests
{
    public class ControlInputTests : InputTestFixture
    {
        private const string SaveKey = "TNDA.ControlType";
        private bool saved;
        private int value;

        public override void Setup()
        {
            base.Setup();
            saved = PlayerPrefs.HasKey(SaveKey);
            value = PlayerPrefs.GetInt(SaveKey);
            PlayerPrefs.DeleteKey(SaveKey);
        }

        public override void TearDown()
        {
            if (saved) PlayerPrefs.SetInt(SaveKey, value);
            else PlayerPrefs.DeleteKey(SaveKey);
            PlayerPrefs.Save();
            base.TearDown();
        }

        [Test]
        public void DefaultTypeAUsesZForGasInsteadOfTheUpArrow()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            Press(keyboard.zKey);
            Assert.That(ArcadeGame.ReadInput(0).throttle, Is.EqualTo(1));
            Release(keyboard.zKey);
            Press(keyboard.upArrowKey);
            Assert.That(ArcadeGame.ReadInput(0).throttle, Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void KeyboardLayoutMapsGasItemAndBrakeWithoutChangingP2(bool legacy)
        {
            PlayerPrefs.SetInt(SaveKey, legacy ? 1 : 0);
            var keyboard = InputSystem.AddDevice<Keyboard>();
            Press(keyboard[legacy ? Key.UpArrow : Key.Z]);
            Press(keyboard[legacy ? Key.LeftCtrl : Key.X]);
            Press(keyboard[legacy ? Key.DownArrow : Key.A]);
            Press(keyboard.leftArrowKey);
            var p1 = ArcadeGame.ReadInput(0);
            Assert.That(p1.throttle, Is.EqualTo(1));
            Assert.That(p1.action && p1.brake, Is.True);
            Assert.That(p1.steer, Is.EqualTo(-1));
            Release(keyboard[legacy ? Key.LeftCtrl : Key.X]);
            Press(keyboard[legacy ? Key.LeftCtrl : Key.X]);
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

        [TestCase(false, 0)]
        [TestCase(false, 1)]
        [TestCase(true, 0)]
        [TestCase(true, 1)]
        public void EachPadUsesItsSelectedLayoutAndOnlyControlsItsOwnSeat(bool legacy, int seat)
        {
            PlayerPrefs.SetInt(SaveKey, legacy ? 1 : 0);
            var first = InputSystem.AddDevice<Gamepad>();
            var second = InputSystem.AddDevice<Gamepad>();
            var pad = seat == 0 ? first : second;
            if (legacy) Set(pad.leftStick, new Vector2(.5f, 1));
            else { Press(pad.buttonSouth); Set(pad.leftStick, new Vector2(.5f, 1)); }
            Press(legacy ? pad.buttonSouth : pad.buttonEast);
            var input = ArcadeGame.ReadInput(seat);
            Assert.That(input.throttle, Is.EqualTo(1));
            Assert.That(input.action, Is.True);
            Release(legacy ? pad.buttonSouth : pad.buttonEast);
            Press(legacy ? pad.buttonSouth : pad.buttonEast);
            Assert.That(ArcadeGame.ReadInput(seat).actionPressed, Is.True);
            Assert.That(input.steer, Is.GreaterThan(0));
            Assert.That(ArcadeGame.ReadInput(1 - seat).throttle, Is.Zero);
            Assert.That(ArcadeGame.ReadInput(1 - seat).action, Is.False);
            if (legacy) Set(pad.leftStick, Vector2.down);
            else Press(pad.buttonWest);
            Assert.That(ArcadeGame.ReadInput(seat).brake, Is.True);
            if (!legacy)
            {
                Release(pad.buttonSouth);
                Assert.That(ArcadeGame.ReadInput(seat).throttle, Is.Zero, "stick up never accelerates Type A");
            }
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
