using System.Collections.Generic;
using NUnit.Framework;

namespace Loading.Tests
{
    public class LoadingRunnerTests
    {
        private readonly List<LoadingResult> _results = new();
        private LoadingRunner _runner;

        [SetUp]
        public void SetUp()
        {
            _results.Clear();
            _runner = new LoadingRunner();
        }

        private void Record(LoadingResult result) => _results.Add(result);

        [Test]
        public void Runner_New_NotRunning()
        {
            Assert.IsFalse(_runner.IsRunning);
            Assert.AreEqual(0f, _runner.Progress);
            Assert.AreEqual(0f, _runner.Elapsed);
        }

        [Test]
        public void Runner_Run_StartsRootAndRuns()
        {
            var root = new FakeOperation();

            bool started = _runner.Run(root, Record);

            Assert.IsTrue(started);
            Assert.IsTrue(_runner.IsRunning);
            Assert.AreEqual(LoadingStatus.Running, root.Status);
        }

        [Test]
        public void Runner_RunWhileRunning_ReturnsFalseAndKeepsFirst()
        {
            var first = new FakeOperation();
            var second = new FakeOperation();
            _runner.Run(first, Record);

            bool started = _runner.Run(second, Record);

            Assert.IsFalse(started);
            Assert.AreEqual(LoadingStatus.Idle, second.Status);
        }

        [Test]
        public void Runner_Tick_PassesTimeAndCountsElapsed()
        {
            var root = new FakeOperation();
            _runner.Run(root, Record);

            _runner.Tick(0.25f);
            _runner.Tick(0.5f);

            Assert.AreEqual(0.75f, root.TickedTime, 1e-5f);
            Assert.AreEqual(0.75f, _runner.Elapsed, 1e-5f);
        }

        [Test]
        public void Runner_Progress_FollowsRoot()
        {
            var root = new FakeOperation { Measured = 0.3f };
            _runner.Run(root, Record);

            Assert.AreEqual(0.3f, _runner.Progress, 1e-5f);
        }

        [Test]
        public void Runner_RootSucceeds_DoneOnceWithSuccess()
        {
            var root = new FakeOperation();
            _runner.Run(root, Record);

            root.Finish();
            _runner.Tick(0f);
            _runner.Tick(0f);

            Assert.AreEqual(1, _results.Count);
            Assert.IsTrue(_results[0].Succeeded);
            Assert.IsFalse(_runner.IsRunning);
        }

        [Test]
        public void Runner_RootFails_DoneWithError()
        {
            var root = new FakeOperation();
            _runner.Run(root, Record);

            root.Break("no scene");
            _runner.Tick(0f);

            Assert.AreEqual(1, _results.Count);
            Assert.IsFalse(_results[0].Succeeded);
            Assert.AreEqual("no scene", _results[0].Error);
        }

        [Test]
        public void Runner_InstantRoot_DoneInsideRun()
        {
            bool started = _runner.Run(new ActionOperation(() => { }), Record);

            Assert.IsTrue(started);
            Assert.AreEqual(1, _results.Count);
            Assert.IsFalse(_runner.IsRunning);
        }

        [Test]
        public void Runner_Done_CalledAfterRunningReset()
        {
            var root = new FakeOperation();
            bool runningInDone = true;
            _runner.Run(root, _ => runningInDone = _runner.IsRunning);

            root.Finish();
            _runner.Tick(0f);

            Assert.IsFalse(runningInDone);
        }

        [Test]
        public void Runner_RunFromDone_StartsNextLoading()
        {
            var first = new FakeOperation();
            var second = new FakeOperation();
            bool secondStarted = false;
            _runner.Run(first, _ => secondStarted = _runner.Run(second, Record));

            first.Finish();
            _runner.Tick(0f);

            Assert.IsTrue(secondStarted);
            Assert.IsTrue(_runner.IsRunning);
            Assert.AreEqual(LoadingStatus.Running, second.Status);
        }

        [Test]
        public void Runner_NewRun_ResetsElapsed()
        {
            var first = new FakeOperation();
            _runner.Run(first);
            _runner.Tick(1f);
            first.Finish();
            _runner.Tick(0f);

            _runner.Run(new FakeOperation());

            Assert.AreEqual(0f, _runner.Elapsed);
        }

        [Test]
        public void Runner_NullRoot_Throws()
        {
            Assert.Throws<System.ArgumentNullException>(() => _runner.Run(null));
        }
    }
}
