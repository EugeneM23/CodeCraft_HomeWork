using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Saving.Tests
{
    /// <summary>
    /// A coin flying to the bank pays when it lands: the save system does not save in the middle of it. Saves it makes
    /// by itself wait for the end, quit and pause end it first, a load drops it. Save() from code is not held back.
    /// </summary>
    public class WorkInFlightTests
    {
        /// <summary>The coin: in flight until it lands, then the money is paid.</summary>
        private sealed class FlyingCoin : IWorkInFlight
        {
            private readonly Wallet _wallet;
            public FlyingCoin(Wallet wallet) => _wallet = wallet;

            public bool IsBusy { get; private set; }
            public int Finished { get; private set; }
            public int Dropped { get; private set; }

            public void Launch() => IsBusy = true;

            public void Land()
            {
                if (!IsBusy)
                    return;

                IsBusy = false;
                _wallet.Money += 10;
            }

            public void Finish()
            {
                Finished++;
                Land();
            }

            public void Drop()
            {
                Dropped++;
                IsBusy = false;
            }
        }

        private sealed class Wallet
        {
            public int Money;
        }

        private GameObject _object;
        private InMemorySaveStorage _storage;
        private SaveLoadSystem _system;
        private Wallet _wallet;
        private FlyingCoin _coin;
        private int _saves;
        private IWorkInFlight _before;

        [SetUp]
        public void SetUp()
        {
            _storage = new InMemorySaveStorage();
            _object = new GameObject("SaveLoadSystem");
            _system = _object.AddComponent<SaveLoadSystem>();
            _system.UseStorage(_storage);
            // Autosave every second of game time, and the clock only moves when a test ticks it
            _system.Configure(saveOnQuit: true, saveOnPause: true, autoSavePeriod: 1f);
            _system.TickManually = true;
            _wallet = new Wallet();
            _coin = new FlyingCoin(_wallet);
            _before = SaveLoadSystem.WorkInFlight;
            SaveLoadSystem.WorkInFlight = _coin;
            _system.Register("wallet", () => _wallet.Money, money => _wallet.Money = money);
            _saves = 0;
            _system.Saved += _ => _saves++;
        }

        [TearDown]
        public void TearDown()
        {
            SaveLoadSystem.WorkInFlight = _before; // DOTween for the tests that follow
            if (_object != null)
                Object.DestroyImmediate(_object);
        }

        private int SavedMoney()
        {
            // What the file holds, read back by a service of its own on the same storage
            var reader = new SaveLoadService(_storage, Application.version);
            int money = -1;
            reader.Register("wallet", () => money, value => money = value);
            reader.Load();
            return money;
        }

        [UnityTest]
        public IEnumerator Button_CoinInFlight_SaveWaitsUntilItLands()
        {
            _coin.Launch();

            _system.SaveGame();
            yield return null;

            Assert.AreEqual(0, _saves, "nothing is written while the coin is in the air");
            Assert.IsTrue(_system.IsSaveWaiting);

            _coin.Land();
            yield return null;

            Assert.AreEqual(1, _saves);
            Assert.IsFalse(_system.IsSaveWaiting);
            Assert.AreEqual(10, SavedMoney(), "the save has the money the coin brought");
        }

        [UnityTest]
        public IEnumerator Autosave_CoinInFlight_WaitsAndSeveralRequestsMakeOneSave()
        {
            _coin.Launch();

            _system.Tick(1.1f);
            _system.SaveGame();
            yield return null;
            Assert.AreEqual(0, _saves);

            _coin.Land();
            yield return null;
            yield return null;

            Assert.AreEqual(1, _saves, "the autosave and the button waited together");
            Assert.AreEqual(10, SavedMoney());
        }

        [UnityTest]
        public IEnumerator Button_NothingInFlight_SavesAtOnce()
        {
            _system.SaveGame();

            Assert.AreEqual(1, _saves);
            Assert.IsFalse(_system.IsSaveWaiting);
            yield break;
        }

        [UnityTest]
        public IEnumerator Code_SaveIsNeverHeldBack()
        {
            _coin.Launch();

            Assert.AreEqual(SaveOutcome.Saved, _system.Save());
            Assert.AreEqual(0, SavedMoney(), "Save() writes the game as it is, the coin still in the air");
            yield break;
        }

        [UnityTest]
        public IEnumerator Quit_CoinInFlight_LandsFirstAndIsInTheSave()
        {
            _coin.Launch();

            _object.SendMessage("OnApplicationQuit");

            Assert.AreEqual(1, _coin.Finished);
            Assert.AreEqual(1, _saves, "the app is closing: nothing to wait for");
            Assert.AreEqual(10, SavedMoney());
            yield break;
        }

        [UnityTest]
        public IEnumerator Pause_CoinInFlight_LandsFirstAndIsInTheSave()
        {
            _coin.Launch();

            _object.SendMessage("OnApplicationPause", true);

            Assert.AreEqual(1, _coin.Finished);
            Assert.AreEqual(10, SavedMoney());
            yield break;
        }

        [UnityTest]
        public IEnumerator Load_CoinInFlight_IsDroppedAndAWaitingSaveIsForgotten()
        {
            _system.Save(); // nothing in the wallet yet
            _coin.Launch();
            _system.SaveGame(); // waits for the coin
            int savesBefore = _saves;

            _system.Load();
            yield return null;
            _coin.Land(); // dropped: lands nowhere
            yield return null;

            Assert.AreEqual(1, _coin.Dropped);
            Assert.AreEqual(0, _wallet.Money, "the coin started after the save does not pay into the loaded game");
            Assert.AreEqual(savesBefore, _saves, "the waiting save is not made over the loaded state");
            Assert.IsFalse(_system.IsSaveWaiting);
        }

        [UnityTest]
        public IEnumerator Off_SavesInTheMiddleAsBefore()
        {
            _system.FinishWorkBeforeSave = false;
            _coin.Launch();

            _system.SaveGame();
            _object.SendMessage("OnApplicationQuit");

            Assert.AreEqual(2, _saves);
            Assert.AreEqual(0, _coin.Finished);
            Assert.AreEqual(0, SavedMoney());
            yield break;
        }

        [UnityTest]
        public IEnumerator Waiting_WorkThatNeverEnds_SavesAfterTheLongestWait()
        {
            _coin.Launch(); // never lands

            _system.SaveGame();
            yield return new WaitForSecondsRealtime(SaveLoadSystem.MAX_WAIT_FOR_WORK + 0.2f);

            Assert.AreEqual(1, _saves, "a save is late at worst, never lost");
            Assert.AreEqual(0, SavedMoney());
        }
    }
}
