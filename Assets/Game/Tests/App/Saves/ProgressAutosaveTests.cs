using NUnit.Framework;
using Saving;

namespace Game.Tests
{
    public sealed class ProgressAutosaveTests
    {
        private InMemorySaveStorage _storage;
        private PlayerProgress _progress;
        private ProgressAutosave _autosave;

        [SetUp]
        public void SetUp()
        {
            _storage = new InMemorySaveStorage();
            _progress = new PlayerProgress(_storage, "1.0");
            _progress.Load();
            _autosave = new ProgressAutosave(_progress, flow: null, delaySeconds: 3f);
        }

        [TearDown]
        public void TearDown() => _autosave.Dispose();

        private bool Written => _storage.Has(0, SaveFiles.SAVE);

        [Test]
        public void Change_IsWritten_OnlyAfterTheDelay()
        {
            _progress.AddCredits(5);

            _autosave.Tick(2.9f);
            Assert.IsFalse(Written);

            _autosave.Tick(0.2f);
            Assert.IsTrue(Written);
            Assert.IsFalse(_progress.IsDirty);
        }

        [Test]
        public void NewChange_RestartsTheDelay()
        {
            _progress.AddCredits(5);
            _autosave.Tick(2f);
            _progress.AddCredits(5);

            _autosave.Tick(2f);
            Assert.IsFalse(Written);

            _autosave.Tick(1.5f);
            Assert.IsTrue(Written);
        }

        [Test]
        public void ChangesThatNeverStop_AreWrittenWithinTenDelays()
        {
            // A change every 2 s restarts the 3 s delay forever; the cap writes after 30 s
            for (int i = 0; i < 14; i++)
            {
                _progress.AddCredits(1);
                _autosave.Tick(2f);
                Assert.IsFalse(Written, $"written too early, at {(i + 1) * 2} s");
            }

            _progress.AddCredits(1);
            _autosave.Tick(2f);
            Assert.IsTrue(Written);
        }

        [Test]
        public void SaveNow_WritesADirtyProgressAtOnce()
        {
            _progress.UnlockLevel("level_01");

            _autosave.SaveNow();

            Assert.IsTrue(Written);
            Assert.IsFalse(_progress.IsDirty);
        }

        [Test]
        public void Load_RaisesChanged_ButWritesNothing()
        {
            _progress.Load();

            _autosave.Tick(10f);
            _autosave.SaveNow();

            Assert.IsFalse(Written);
        }

        [Test]
        public void AfterDispose_ChangesAreNotWritten()
        {
            _autosave.Dispose();
            _progress.AddCredits(5);

            _autosave.Tick(10f);

            Assert.IsFalse(Written);
        }
    }
}
