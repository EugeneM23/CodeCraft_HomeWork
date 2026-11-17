using NUnit.Framework;
using Saving.Objects;

namespace Saving.Tests
{
    /// <summary>
    /// The module's PlayMode tests make their own save systems: in a project whose settings sit in Resources, the test
    /// scenes' Saveables would make an automatic one first. Off for the whole assembly; SaveSystemAutoCreateTests turn it
    /// on per test and back off after.
    /// </summary>
    [SetUpFixture]
    public class AutoCreateOff
    {
        [OneTimeSetUp]
        public void TurnOff() => SaveSystemAutoCreate.FindSettings = () => null;

        [OneTimeTearDown]
        public void Restore() => SaveSystemAutoCreate.Reset();
    }
}
