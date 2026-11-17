using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.InputSystem;
using KeySlot = Game.KeyRebindUseCase.KeySlot;

namespace Game.Tests
{
    public sealed class KeyRebindUseCaseTests
    {
        private const string W = "<Keyboard>/w";
        private const string LEFT_SHIFT = "<Keyboard>/leftShift";

        private GameControls _controls;

        [SetUp]
        public void SetUp() => _controls = new GameControls();

        // GameControls.Dispose calls Object.Destroy, which edit mode refuses: destroy its asset directly.
        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(_controls.asset);

        private KeySlot Part(string part) =>
            new(_controls.Tank.Drive, KeyRebindUseCase.FindCompositePart(_controls.Tank.Drive, "WASD", part));

        private static KeySlot Plain(InputAction action) => new(action, KeyRebindUseCase.FindFirstPlainBinding(action));

        [Test]
        public void FindCompositePart_UpOfWasd_IsTheWKey()
        {
            int index = KeyRebindUseCase.FindCompositePart(_controls.Tank.Drive, "WASD", "up");

            Assert.That(_controls.Tank.Drive.bindings[index].path, Is.EqualTo(W));
        }

        [Test]
        public void FindCompositePart_PartsOfWasd_AreNotTheArrows()
        {
            int down = KeyRebindUseCase.FindCompositePart(_controls.Tank.Drive, "WASD", "down");
            int right = KeyRebindUseCase.FindCompositePart(_controls.Tank.Drive, "WASD", "right");

            Assert.That(_controls.Tank.Drive.bindings[down].path, Is.EqualTo("<Keyboard>/s"));
            Assert.That(_controls.Tank.Drive.bindings[right].path, Is.EqualTo("<Keyboard>/d"));
        }

        [Test]
        public void FindCompositePart_UnknownComposite_ReturnsMinusOne()
        {
            Assert.That(KeyRebindUseCase.FindCompositePart(_controls.Tank.Drive, "Gamepad", "up"), Is.EqualTo(-1));
        }

        [Test]
        public void FindFirstPlainBinding_Boost_IsLeftShift()
        {
            int index = KeyRebindUseCase.FindFirstPlainBinding(_controls.Tank.Boost);

            Assert.That(_controls.Tank.Boost.bindings[index].path, Is.EqualTo(LEFT_SHIFT));
        }

        [Test]
        public void FindConflict_KeyOfAnotherRowOfTheSameMap_FindsThatRow()
        {
            List<KeySlot> slots = new() { Part("up"), Part("down"), Plain(_controls.Tank.Boost) };
            _controls.Tank.Boost.ApplyBindingOverride(slots[2].BindingIndex, W);

            Assert.That(KeyRebindUseCase.FindConflict(slots, 2), Is.EqualTo(0));
        }

        [Test]
        public void FindConflict_FreeKey_ReturnsMinusOne()
        {
            List<KeySlot> slots = new() { Part("up"), Plain(_controls.Tank.Boost) };
            _controls.Tank.Boost.ApplyBindingOverride(slots[1].BindingIndex, "<Keyboard>/q");

            Assert.That(KeyRebindUseCase.FindConflict(slots, 1), Is.EqualTo(-1));
        }

        [Test]
        public void FindConflict_SameKeyInAnotherMap_IsNoConflict()
        {
            List<KeySlot> slots = new() { Plain(_controls.Tank.Boost), Plain(_controls.Sandbox.AirStrike) };
            _controls.Sandbox.AirStrike.ApplyBindingOverride(slots[1].BindingIndex, LEFT_SHIFT);

            Assert.That(KeyRebindUseCase.FindConflict(slots, 1), Is.EqualTo(-1));
        }

        [Test]
        public void ResolveConflict_TakenKey_GivesTheOtherRowTheOldKey()
        {
            List<KeySlot> slots = new() { Part("up"), Plain(_controls.Tank.Boost) };
            _controls.Tank.Boost.ApplyBindingOverride(slots[1].BindingIndex, W);

            int swapped = KeyRebindUseCase.ResolveConflict(slots, 1, LEFT_SHIFT);

            Assert.That(swapped, Is.EqualTo(0));
            Assert.That(slots[0].EffectivePath, Is.EqualTo(LEFT_SHIFT));
            Assert.That(slots[1].EffectivePath, Is.EqualTo(W));
        }

        [Test]
        public void ResolveConflict_FreeKey_ChangesNothingElse()
        {
            List<KeySlot> slots = new() { Part("up"), Plain(_controls.Tank.Boost) };
            _controls.Tank.Boost.ApplyBindingOverride(slots[1].BindingIndex, "<Keyboard>/q");

            Assert.That(KeyRebindUseCase.ResolveConflict(slots, 1, LEFT_SHIFT), Is.EqualTo(-1));
            Assert.That(slots[0].EffectivePath, Is.EqualTo(W));
        }

        [Test]
        public void SetPath_BackToTheAssetKey_DropsTheOverride()
        {
            KeySlot up = Part("up");
            _controls.Tank.Drive.ApplyBindingOverride(up.BindingIndex, "<Keyboard>/q");

            KeyRebindUseCase.SetPath(up, W);

            Assert.That(_controls.Tank.Drive.bindings[up.BindingIndex].hasOverrides, Is.False);
            Assert.That(up.EffectivePath, Is.EqualTo(W));
        }

        [Test]
        public void Overrides_SavedAndLoadedThroughGameInput_ComeBack()
        {
            GameInput input = new();
            try
            {
                InputAction fire = input.Controls.Tank.FireMain;
                int index = KeyRebindUseCase.FindFirstPlainBinding(fire);
                fire.ApplyBindingOverride(index, "<Keyboard>/space");
                string json = input.SaveOverrides();

                input.LoadOverrides("");
                Assert.That(fire.bindings[index].effectivePath, Is.EqualTo("<Mouse>/leftButton"));

                input.LoadOverrides(json);
                Assert.That(fire.bindings[index].effectivePath, Is.EqualTo("<Keyboard>/space"));
            }
            finally
            {
                input.Controls.Disable();
                UnityEngine.Object.DestroyImmediate(input.Controls.asset);
            }
        }
    }
}
