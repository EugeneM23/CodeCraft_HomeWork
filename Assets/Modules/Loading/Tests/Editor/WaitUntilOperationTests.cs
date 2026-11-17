using System;
using NUnit.Framework;

namespace Loading.Tests
{
    public class WaitUntilOperationTests
    {
        [Test]
        public void WaitUntil_ConditionTrue_SucceedsInBegin()
        {
            var operation = new WaitUntilOperation(() => true);

            operation.Begin();

            Assert.AreEqual(LoadingStatus.Succeeded, operation.Status);
        }

        [Test]
        public void WaitUntil_ConditionLater_SucceedsOnTick()
        {
            bool ready = false;
            var operation = new WaitUntilOperation(() => ready);

            operation.Begin();
            operation.Tick(0.1f);
            Assert.AreEqual(LoadingStatus.Running, operation.Status, "waits while the condition is false");

            ready = true;
            operation.Tick(0.1f);

            Assert.AreEqual(LoadingStatus.Succeeded, operation.Status);
        }

        [Test]
        public void WaitUntil_ConditionThrows_Fails()
        {
            var operation = new WaitUntilOperation(() => throw new InvalidOperationException("no data"));

            operation.Begin();

            Assert.AreEqual(LoadingStatus.Failed, operation.Status);
            StringAssert.Contains("no data", operation.Error);
        }

        [Test]
        public void WaitUntil_NullCondition_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new WaitUntilOperation(null));
        }

        [Test]
        public void WaitUntil_DefaultWeight_IsZero()
        {
            Assert.AreEqual(0f, new WaitUntilOperation(() => true).Weight);
            Assert.AreEqual(2f, new WaitUntilOperation(() => true, 2f).Weight);
        }
    }
}
