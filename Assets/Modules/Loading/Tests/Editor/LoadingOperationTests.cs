using System;
using NUnit.Framework;

namespace Loading.Tests
{
    public class LoadingOperationTests
    {
        [Test]
        public void Operation_New_IsIdleWithoutProgress()
        {
            var operation = new FakeOperation();

            Assert.AreEqual(LoadingStatus.Idle, operation.Status);
            Assert.IsFalse(operation.IsDone);
            Assert.AreEqual(0f, operation.Progress);
            Assert.IsNull(operation.Error);
        }

        [Test]
        public void Operation_NegativeWeight_ClampedToZero()
        {
            Assert.AreEqual(0f, new FakeOperation(-3f).Weight);
            Assert.AreEqual(2.5f, new FakeOperation(2.5f).Weight);
        }

        [Test]
        public void Operation_Begin_RunsAndReportsMeasuredProgress()
        {
            var operation = new FakeOperation { Measured = 0.4f };

            operation.Begin();

            Assert.AreEqual(LoadingStatus.Running, operation.Status);
            Assert.AreEqual(0.4f, operation.Progress, 1e-5f);
        }

        [Test]
        public void Operation_MeasuredOutOfRange_ProgressClamped()
        {
            var operation = new FakeOperation { Measured = 7f };
            operation.Begin();

            Assert.AreEqual(1f, operation.Progress);

            operation.Measured = -1f;
            Assert.AreEqual(0f, operation.Progress);
        }

        [Test]
        public void Operation_Succeeded_ProgressIsOne()
        {
            var operation = new FakeOperation { Measured = 0.1f };
            operation.Begin();

            operation.Finish();

            Assert.AreEqual(LoadingStatus.Succeeded, operation.Status);
            Assert.IsTrue(operation.IsDone);
            Assert.AreEqual(1f, operation.Progress);
        }

        [Test]
        public void Operation_Fail_KeepsErrorText()
        {
            var operation = new FakeOperation();
            operation.Begin();

            operation.Break("no disk");

            Assert.AreEqual(LoadingStatus.Failed, operation.Status);
            Assert.IsTrue(operation.IsDone);
            Assert.AreEqual("no disk", operation.Error);
            Assert.IsNull(operation.Exception);
        }

        [Test]
        public void Operation_SucceedAfterFail_StaysFailed()
        {
            var operation = new FakeOperation();
            operation.Begin();
            operation.Break("broken");

            operation.Finish();

            Assert.AreEqual(LoadingStatus.Failed, operation.Status);
            Assert.AreEqual("broken", operation.Error);
        }

        [Test]
        public void Operation_ThrowsOnBegin_FailsWithTypeAndMessage()
        {
            var exception = new InvalidOperationException("boom");
            var operation = new FakeOperation { ThrowOnBegin = exception };

            operation.Begin();

            Assert.AreEqual(LoadingStatus.Failed, operation.Status);
            StringAssert.Contains(nameof(FakeOperation), operation.Error);
            StringAssert.Contains("boom", operation.Error);
            Assert.AreSame(exception, operation.Exception);
        }

        [Test]
        public void Operation_ThrowsOnTick_Fails()
        {
            var operation = new FakeOperation { ThrowOnTick = new Exception("tick boom") };
            operation.Begin();

            operation.Tick(0.1f);

            Assert.AreEqual(LoadingStatus.Failed, operation.Status);
            StringAssert.Contains("tick boom", operation.Error);
        }

        [Test]
        public void Operation_Tick_PassesTimeOnlyWhileRunning()
        {
            var operation = new FakeOperation();

            operation.Tick(1f);
            operation.Begin();
            operation.Tick(0.25f);
            operation.Finish();
            operation.Tick(1f);

            Assert.AreEqual(1, operation.Ticks);
            Assert.AreEqual(0.25f, operation.TickedTime, 1e-5f);
        }

        [Test]
        public void Operation_BeginWhileRunning_Throws()
        {
            var operation = new FakeOperation();
            operation.Begin();

            Assert.Throws<InvalidOperationException>(operation.Begin);
        }

        [Test]
        public void Operation_BeginAfterFail_StartsOverClean()
        {
            var operation = new FakeOperation();
            operation.Begin();
            operation.Break("first");

            operation.Begin();

            Assert.AreEqual(LoadingStatus.Running, operation.Status);
            Assert.IsNull(operation.Error);
            Assert.AreEqual(0f, operation.Progress);
        }

        [Test]
        public void ActionOperation_Begin_CallsActionAndSucceeds()
        {
            int calls = 0;
            var operation = new ActionOperation(() => calls++);

            operation.Begin();

            Assert.AreEqual(1, calls);
            Assert.AreEqual(LoadingStatus.Succeeded, operation.Status);
            Assert.AreEqual(0f, operation.Weight);
        }

        [Test]
        public void ActionOperation_NullAction_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new ActionOperation(null));
        }

        [Test]
        public void ActionOperation_ActionThrows_Fails()
        {
            var operation = new ActionOperation(() => throw new Exception("bad action"));

            operation.Begin();

            Assert.AreEqual(LoadingStatus.Failed, operation.Status);
            StringAssert.Contains("bad action", operation.Error);
        }

        [Test]
        public void LoadingResult_Factories_DescribeOutcome()
        {
            Assert.IsTrue(LoadingResult.Success.Succeeded);
            Assert.IsNull(LoadingResult.Success.Error);

            LoadingResult failure = LoadingResult.Failure("oops");
            Assert.IsFalse(failure.Succeeded);
            Assert.AreEqual("oops", failure.Error);
        }
    }
}
