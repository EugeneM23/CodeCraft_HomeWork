using Loading;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class WarmUpCodeOperationTests
    {
        private static readonly System.Reflection.Assembly Module = typeof(LoadingRunner).Assembly;

        [Test]
        public void AnAssembly_IsCompiledWithinTheFrameBudget_AndOnlyOnceASession()
        {
            var first = new WarmUpCodeOperation(new[] { Module }, frameBudgetMs: 1d);
            first.Begin();
            int ticks = 0;
            while (first.Status == LoadingStatus.Running && ticks < 10000)
            {
                first.Tick(0.016f);
                ticks++;
            }

            Assert.AreEqual(LoadingStatus.Succeeded, first.Status);
            Assert.AreEqual(first.Total, first.Compiled, "every collected method went through");

            // The same session: nothing left to compile, done at once
            var second = new WarmUpCodeOperation(new[] { Module });
            second.Begin();
            Assert.AreEqual(LoadingStatus.Succeeded, second.Status);
            Assert.AreEqual(0, second.Total);
        }

        [Test]
        public void NoAssemblies_SucceedsAtOnce()
        {
            var operation = new WarmUpCodeOperation(null);
            operation.Begin();

            Assert.AreEqual(LoadingStatus.Succeeded, operation.Status);
            Assert.AreEqual(0, operation.Total);
        }
    }
}
