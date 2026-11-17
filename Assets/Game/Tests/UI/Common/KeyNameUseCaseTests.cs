using UnityEngine.InputSystem;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class KeyNameUseCaseTests
    {
        private GameControls _controls;

        [SetUp]
        public void SetUp() => _controls = new GameControls();

        // GameControls.Dispose calls Object.Destroy, which edit mode refuses: destroy its asset directly.
        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(_controls.asset);

        [Test]
        public void OfPath_LetterKey_IsTheUppercaseLatinLetter()
        {
            Assert.That(KeyNameUseCase.OfPath("<Keyboard>/w"), Is.EqualTo("W"));
        }

        [Test]
        public void OfAction_Boost_IsLeftShift()
        {
            Assert.That(KeyNameUseCase.OfAction(_controls.Tank.Boost), Is.EqualTo("Left Shift"));
        }

        [Test]
        public void OfAction_Pause_IsEscape()
        {
            Assert.That(KeyNameUseCase.OfAction(_controls.Tank.Pause), Is.EqualTo("Escape"));
        }

        [Test]
        public void OfAction_MouseButtons_ComeFromTheStringTable()
        {
            Assert.That(KeyNameUseCase.OfAction(_controls.Tank.FireMain), Is.EqualTo(UiStrings.Get("key.lmb")));
            Assert.That(KeyNameUseCase.OfAction(_controls.Tank.FireSecondary), Is.EqualTo(UiStrings.Get("key.rmb")));
            Assert.That(KeyNameUseCase.OfPath("<Mouse>/middleButton"), Is.EqualTo(UiStrings.Get("key.mmb")));
        }

        [Test]
        public void OfBinding_WasdComposite_IsItsPartsInReadingOrder()
        {
            Assert.That(KeyNameUseCase.OfBinding(_controls.Tank.Drive, 0), Is.EqualTo("W/A/S/D"));
        }

        [Test]
        public void OfBinding_CompositePart_IsThatKey()
        {
            int up = KeyRebindUseCase.FindCompositePart(_controls.Tank.Drive, "WASD", "up");

            Assert.That(KeyNameUseCase.OfBinding(_controls.Tank.Drive, up), Is.EqualTo("W"));
        }

        [Test]
        public void OfBinding_Override_ShowsTheNewKey()
        {
            int up = KeyRebindUseCase.FindCompositePart(_controls.Tank.Drive, "WASD", "up");
            _controls.Tank.Drive.ApplyBindingOverride(up, "<Keyboard>/q");

            Assert.That(KeyNameUseCase.OfBinding(_controls.Tank.Drive, 0), Is.EqualTo("Q/A/S/D"));
        }

        [Test]
        public void OfPath_Empty_IsADash()
        {
            Assert.That(KeyNameUseCase.OfPath(""), Is.EqualTo(KeyNameUseCase.EMPTY));
        }
    }
}
