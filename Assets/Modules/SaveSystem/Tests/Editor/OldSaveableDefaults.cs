using NUnit.Framework;
using Saving.Objects;

namespace Saving.Tests
{
    /// <summary>
    /// A Saveable added in the editor starts with an explicit list (its own Transform, body and scripts). The module's
    /// editor tests were written for the Saveable before the list — everything that can be saved unless unchecked —
    /// and that way stays supported for old scenes and prefabs: off for the whole assembly, so they keep testing it.
    /// SaveableListTests turn the new start on per test.
    /// </summary>
    [SetUpFixture]
    public class OldSaveableDefaults
    {
        [OneTimeSetUp]
        public void TurnOff() => Saveable.DefaultsOnAdd = false;

        [OneTimeTearDown]
        public void Restore() => Saveable.DefaultsOnAdd = true;
    }
}
