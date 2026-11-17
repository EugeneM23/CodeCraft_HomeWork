using NUnit.Framework;

namespace AIKit.Tests
{
    /// <summary>After every test of the module's EditMode assembly: the hidden objects of the graph rigs are destroyed.</summary>
    [SetUpFixture]
    public sealed class GraphRigCleanup
    {
        [OneTimeTearDown]
        public void DestroyRigs() => GraphRig.DestroyAll();
    }
}
