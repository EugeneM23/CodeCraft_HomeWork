using NUnit.Framework;

namespace Loading.Tests
{
    /// <summary>Fail outside of Running is ignored: an operation that is done, or not begun, keeps its status.</summary>
    public class LoadingOperationFinishTests
    {
        [Test]
        public void Operation_FailAfterSucceed_StaysSucceeded()
        {
            var operation = new FakeOperation();
            operation.Begin();
            operation.Finish();

            operation.Break("too late");

            Assert.AreEqual(LoadingStatus.Succeeded, operation.Status);
            Assert.IsNull(operation.Error);
            Assert.AreEqual(1f, operation.Progress);
        }

        [Test]
        public void Operation_FailBeforeBegin_StaysIdle()
        {
            var operation = new FakeOperation();

            operation.Break("not started");

            Assert.AreEqual(LoadingStatus.Idle, operation.Status);
            Assert.IsNull(operation.Error);
        }

        [Test]
        public void Operation_FailTwice_KeepsFirstError()
        {
            var operation = new FakeOperation();
            operation.Begin();

            operation.Break("first");
            operation.Break("second");

            Assert.AreEqual(LoadingStatus.Failed, operation.Status);
            Assert.AreEqual("first", operation.Error);
        }
    }
}
