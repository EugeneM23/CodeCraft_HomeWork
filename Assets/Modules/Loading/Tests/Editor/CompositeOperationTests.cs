using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Loading.Tests
{
    public class CompositeOperationTests
    {
        private readonly List<string> _log = new();

        [SetUp]
        public void SetUp() => _log.Clear();

        private FakeOperation Fake(string name, float weight = 1f) => new(weight, _log, name);

        [Test]
        public void Sequence_Begin_StartsOnlyFirst()
        {
            FakeOperation a = Fake("a");
            FakeOperation b = Fake("b");
            var sequence = new SequenceOperation(a, b);

            sequence.Begin();

            Assert.AreEqual(LoadingStatus.Running, a.Status);
            Assert.AreEqual(LoadingStatus.Idle, b.Status);
        }

        [Test]
        public void Sequence_ChildFinished_StartsNextOnSameTick()
        {
            FakeOperation a = Fake("a");
            FakeOperation b = Fake("b");
            var sequence = new SequenceOperation(a, b);
            sequence.Begin();

            a.Finish();
            sequence.Tick(0.1f);

            // A finished child is not ticked any more
            CollectionAssert.AreEqual(new[] { "begin a", "begin b" }, _log);
            Assert.AreEqual(LoadingStatus.Running, sequence.Status);
        }

        [Test]
        public void Sequence_InstantChildren_ChainInBegin()
        {
            FakeOperation a = Fake("a");
            FakeOperation b = Fake("b");
            a.SucceedOnBegin = true;
            b.SucceedOnBegin = true;
            var sequence = new SequenceOperation(a, b);

            sequence.Begin();

            Assert.AreEqual(LoadingStatus.Succeeded, sequence.Status);
            CollectionAssert.AreEqual(new[] { "begin a", "begin b" }, _log);
        }

        [Test]
        public void Sequence_AllFinished_Succeeds()
        {
            FakeOperation a = Fake("a");
            FakeOperation b = Fake("b");
            var sequence = new SequenceOperation(a, b);
            sequence.Begin();

            a.Finish();
            sequence.Tick(0f);
            b.Finish();
            sequence.Tick(0f);

            Assert.AreEqual(LoadingStatus.Succeeded, sequence.Status);
        }

        [Test]
        public void Sequence_ChildFails_StopsWithItsError()
        {
            FakeOperation a = Fake("a");
            FakeOperation b = Fake("b");
            var sequence = new SequenceOperation(a, b);
            sequence.Begin();

            a.Break("scene missing");
            sequence.Tick(0f);

            Assert.AreEqual(LoadingStatus.Failed, sequence.Status);
            Assert.AreEqual("scene missing", sequence.Error);
            Assert.AreEqual(LoadingStatus.Idle, b.Status);
        }

        [Test]
        public void Sequence_ChildThrows_FailsAndKeepsException()
        {
            FakeOperation a = Fake("a");
            var exception = new Exception("boom");
            a.ThrowOnTick = exception;
            var sequence = new SequenceOperation(a);
            sequence.Begin();

            sequence.Tick(0f);

            Assert.AreEqual(LoadingStatus.Failed, sequence.Status);
            StringAssert.Contains("boom", sequence.Error);
            Assert.AreSame(exception, sequence.Exception);
        }

        [Test]
        public void Sequence_Weight_IsSumOfChildren()
        {
            var sequence = new SequenceOperation(Fake("a", 1f), Fake("b", 3f), Fake("c", 0f));

            Assert.AreEqual(4f, sequence.Weight);
        }

        [Test]
        public void Sequence_Progress_WeightedByChildren()
        {
            FakeOperation a = Fake("a", 1f);
            FakeOperation b = Fake("b", 3f);
            var sequence = new SequenceOperation(a, b);
            sequence.Begin();

            a.Finish();
            sequence.Tick(0f);
            b.Measured = 0.5f;

            // (1 * 1 + 3 * 0.5) / 4
            Assert.AreEqual(0.625f, sequence.Progress, 1e-5f);
        }

        [Test]
        public void Sequence_ZeroWeight_ProgressZeroUntilDone()
        {
            FakeOperation a = Fake("a", 0f);
            var sequence = new SequenceOperation(a);
            sequence.Begin();
            a.Measured = 0.7f;

            Assert.AreEqual(0f, sequence.Progress);

            a.Finish();
            sequence.Tick(0f);
            Assert.AreEqual(1f, sequence.Progress);
        }

        [Test]
        public void Sequence_Empty_SucceedsAtOnce()
        {
            var sequence = new SequenceOperation();

            sequence.Begin();

            Assert.AreEqual(LoadingStatus.Succeeded, sequence.Status);
        }

        [Test]
        public void Sequence_NullChild_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new SequenceOperation(Fake("a"), null));
        }

        [Test]
        public void Sequence_Nested_RunsInOrder()
        {
            FakeOperation a = Fake("a");
            FakeOperation b = Fake("b");
            FakeOperation c = Fake("c");
            var sequence = new SequenceOperation(new SequenceOperation(a, b), c);
            sequence.Begin();

            a.Finish();
            sequence.Tick(0f);
            b.Finish();
            sequence.Tick(0f);

            Assert.AreEqual(LoadingStatus.Running, c.Status);
            Assert.AreEqual(3f, sequence.Weight);
        }

        [Test]
        public void Sequence_BeginAgain_RestartsChildren()
        {
            FakeOperation a = Fake("a");
            var sequence = new SequenceOperation(a);
            sequence.Begin();
            a.Finish();
            sequence.Tick(0f);

            sequence.Begin();

            Assert.AreEqual(LoadingStatus.Running, sequence.Status);
            Assert.AreEqual(LoadingStatus.Running, a.Status);
        }

        [Test]
        public void Parallel_Begin_StartsAll()
        {
            FakeOperation a = Fake("a");
            FakeOperation b = Fake("b");
            var parallel = new ParallelOperation(a, b);

            parallel.Begin();

            Assert.AreEqual(LoadingStatus.Running, a.Status);
            Assert.AreEqual(LoadingStatus.Running, b.Status);
        }

        [Test]
        public void Parallel_Tick_TicksOnlyRunning()
        {
            FakeOperation a = Fake("a");
            FakeOperation b = Fake("b");
            var parallel = new ParallelOperation(a, b);
            parallel.Begin();
            a.Finish();

            parallel.Tick(0.5f);

            Assert.AreEqual(0, a.Ticks);
            Assert.AreEqual(1, b.Ticks);
            Assert.AreEqual(LoadingStatus.Running, parallel.Status);
        }

        [Test]
        public void Parallel_AllFinished_Succeeds()
        {
            FakeOperation a = Fake("a");
            FakeOperation b = Fake("b");
            var parallel = new ParallelOperation(a, b);
            parallel.Begin();

            b.Finish();
            parallel.Tick(0f);
            a.Finish();
            parallel.Tick(0f);

            Assert.AreEqual(LoadingStatus.Succeeded, parallel.Status);
        }

        [Test]
        public void Parallel_OneFails_FailsWithItsError()
        {
            FakeOperation a = Fake("a");
            FakeOperation b = Fake("b");
            var parallel = new ParallelOperation(a, b);
            parallel.Begin();

            b.Break("bank missing");
            parallel.Tick(0f);

            Assert.AreEqual(LoadingStatus.Failed, parallel.Status);
            Assert.AreEqual("bank missing", parallel.Error);
        }

        [Test]
        public void Parallel_FailsOnBegin_FailsAtOnce()
        {
            FakeOperation a = Fake("a");
            a.FailOnBegin = "bad";
            var parallel = new ParallelOperation(a, Fake("b"));

            parallel.Begin();

            Assert.AreEqual(LoadingStatus.Failed, parallel.Status);
            Assert.AreEqual("bad", parallel.Error);
        }

        [Test]
        public void Parallel_InstantChildren_SucceedInBegin()
        {
            FakeOperation a = Fake("a");
            a.SucceedOnBegin = true;
            var parallel = new ParallelOperation(a, new ActionOperation(() => { }));

            parallel.Begin();

            Assert.AreEqual(LoadingStatus.Succeeded, parallel.Status);
        }

        [Test]
        public void Parallel_Progress_WeightedByChildren()
        {
            FakeOperation a = Fake("a", 2f);
            FakeOperation b = Fake("b", 2f);
            var parallel = new ParallelOperation(a, b);
            parallel.Begin();

            a.Measured = 0.5f;
            b.Measured = 0.25f;

            Assert.AreEqual(4f, parallel.Weight);
            Assert.AreEqual(0.375f, parallel.Progress, 1e-5f);
        }

        [Test]
        public void Parallel_Empty_SucceedsAtOnce()
        {
            var parallel = new ParallelOperation();

            parallel.Begin();

            Assert.AreEqual(LoadingStatus.Succeeded, parallel.Status);
        }

        [Test]
        public void Parallel_InsideSequence_WaitsForAll()
        {
            FakeOperation a = Fake("a");
            FakeOperation b = Fake("b");
            FakeOperation c = Fake("c");
            var sequence = new SequenceOperation(new ParallelOperation(a, b), c);
            sequence.Begin();

            a.Finish();
            sequence.Tick(0f);
            Assert.AreEqual(LoadingStatus.Idle, c.Status);

            b.Finish();
            sequence.Tick(0f);
            Assert.AreEqual(LoadingStatus.Running, c.Status);
        }
    }
}
