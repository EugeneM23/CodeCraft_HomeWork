using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class ShopUseCaseTests
    {
        private readonly List<Object> _created = new();
        private ShopCatalogConfig _catalog;
        private TankModuleConfig _cannon;
        private TankModuleConfig _heavyCannon;

        [SetUp]
        public void SetUp()
        {
            _cannon = Module("cannon", TankSlot.Gun, 0, new TankStats { damage = 10 }, 100, new TankStats { damage = 5 });
            _heavyCannon = Module("heavy", TankSlot.Gun, 500, new TankStats { damage = 30, reload = 1f });
            _catalog = Create<ShopCatalogConfig>();
            _catalog.baseStats = new TankStats { health = 100, damage = 50, reload = 3f };
            _catalog.startCredits = 600;
            _catalog.startModules.Add(_cannon);
            _catalog.modules.Add(_cannon);
            _catalog.modules.Add(_heavyCannon);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object item in _created)
                Object.DestroyImmediate(item);
            _created.Clear();
        }

        [Test]
        public void EnsureStarted_NewPlayer_GetsStartCreditsAndInstalledStartModules()
        {
            IPlayerProgress data = new JsonPlayerProgress(null);

            ShopUseCase.EnsureStarted(data, _catalog);

            Assert.AreEqual(600, data.Credits);
            Assert.AreEqual(1, data.GetModuleLevel("cannon"));
            Assert.AreEqual("cannon", data.GetInstalledModule("Gun"));
        }

        [Test]
        public void EnsureStarted_SecondTime_DoesNotGiveCreditsAgain()
        {
            IPlayerProgress data = new JsonPlayerProgress(null);
            ShopUseCase.EnsureStarted(data, _catalog);
            data.TrySpendCredits(595);

            Assert.IsFalse(ShopUseCase.EnsureStarted(data, _catalog));

            Assert.AreEqual(5, data.Credits);
        }

        [Test]
        public void TryBuy_EnoughCredits_TakesThePriceAndOwnsTheModuleWithoutInstallingIt()
        {
            IPlayerProgress data = Started();

            Assert.IsTrue(ShopUseCase.TryBuy(data, _heavyCannon));

            Assert.AreEqual(100, data.Credits);
            Assert.AreEqual(1, data.GetModuleLevel("heavy"));
            Assert.AreEqual("cannon", data.GetInstalledModule("Gun"));
        }

        [Test]
        public void TryBuy_NotEnoughCredits_ChangesNothing()
        {
            IPlayerProgress data = Started();
            data.TrySpendCredits(101);

            Assert.IsFalse(ShopUseCase.TryBuy(data, _heavyCannon));

            Assert.AreEqual(499, data.Credits);
            Assert.AreEqual(0, data.GetModuleLevel("heavy"));
        }

        [Test]
        public void TryBuy_AlreadyOwned_Fails()
        {
            IPlayerProgress data = Started();

            Assert.IsFalse(ShopUseCase.TryBuy(data, _cannon));
            Assert.AreEqual(600, data.Credits);
        }

        [Test]
        public void TryUpgrade_PaysTheNextLevelPriceAndRaisesTheLevel_UntilTheTop()
        {
            IPlayerProgress data = Started();

            Assert.IsTrue(ShopUseCase.TryUpgrade(data, _cannon));
            Assert.AreEqual(500, data.Credits);
            Assert.AreEqual(2, data.GetModuleLevel("cannon"));

            Assert.IsFalse(ShopUseCase.TryUpgrade(data, _cannon), "level 2 is the top");
            Assert.AreEqual(500, data.Credits);
        }

        [Test]
        public void TryInstall_NotOwned_Fails_Owned_ReplacesTheSlot()
        {
            IPlayerProgress data = Started();

            Assert.IsFalse(ShopUseCase.TryInstall(data, _heavyCannon));
            ShopUseCase.TryBuy(data, _heavyCannon);
            Assert.IsTrue(ShopUseCase.TryInstall(data, _heavyCannon));

            Assert.AreEqual("heavy", data.GetInstalledModule("Gun"));
        }

        [Test]
        public void CompleteLevel_PaysTheReward_MarksTheLevel_AndUnlocksTheLevelThatWaitedForIt()
        {
            IPlayerProgress data = Started();
            LevelConfig sandbox = Create<LevelConfig>();
            sandbox.Configure("sandbox", "Sandbox", 0);
            LevelConfig quarry = Create<LevelConfig>();
            quarry.Configure("quarry", "Quarry", 0);
            quarry.Configure(-1, sandbox);
            LevelCatalog levels = Create<LevelCatalog>();
            levels.Configure(string.Empty, sandbox, quarry);
            Assert.IsFalse(LevelUnlockUseCase.IsUnlocked(quarry, data));

            ShopUseCase.CompleteLevel(data, levels, sandbox, 300);

            Assert.AreEqual(900, data.Credits);
            Assert.IsTrue(data.IsLevelCompleted("sandbox"));
            Assert.IsTrue(data.IsLevelUnlocked("quarry"));
            Assert.IsTrue(LevelUnlockUseCase.IsUnlocked(quarry, data));
        }

        [Test]
        public void Compute_AddsInstalledModulesAtTheirLevels_ToTheBase()
        {
            IPlayerProgress data = Started();
            ShopUseCase.TryUpgrade(data, _cannon);

            TankStats stats = TankLoadoutUseCase.Compute(_catalog, data);

            Assert.AreEqual(100, stats.health);
            Assert.AreEqual(65, stats.damage);
        }

        [Test]
        public void Compute_WithAReplacement_CountsItInsteadOfTheInstalledModuleOfItsSlot()
        {
            IPlayerProgress data = Started();

            TankStats stats = TankLoadoutUseCase.Compute(_catalog, data, _heavyCannon);

            Assert.AreEqual(80, stats.damage);
            Assert.AreEqual(4f, stats.reload);
        }

        private IPlayerProgress Started()
        {
            IPlayerProgress data = new JsonPlayerProgress(null);
            ShopUseCase.EnsureStarted(data, _catalog);
            return data;
        }

        private TankModuleConfig Module(string id, TankSlot slot, int price, TankStats level1,
            int upgradePrice = -1, TankStats level2 = default)
        {
            TankModuleConfig module = Create<TankModuleConfig>();
            module.id = id;
            module.slot = slot;
            module.price = price;
            module.levels = new List<TankModuleConfig.Level> { new() { bonus = level1 } };
            if (upgradePrice >= 0)
                module.levels.Add(new TankModuleConfig.Level { price = upgradePrice, bonus = level2 });
            return module;
        }

        private T Create<T>() where T : ScriptableObject
        {
            T item = ScriptableObject.CreateInstance<T>();
            _created.Add(item);
            return item;
        }
    }
}
