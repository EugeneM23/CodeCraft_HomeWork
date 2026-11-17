using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class LevelUnlockUseCaseTests
    {
        private readonly List<Object> _created = new();
        private LevelConfig _sandbox;
        private LevelConfig _quarry;
        private LevelCatalog _catalog;

        [SetUp]
        public void SetUp()
        {
            _sandbox = Create<LevelConfig>();
            _sandbox.Configure("sandbox", "Sandbox", 0);
            _quarry = Create<LevelConfig>();
            _quarry.Configure("quarry", "Quarry", 0);
            _quarry.Configure(-1, _sandbox);
            _catalog = Create<LevelCatalog>();
            _catalog.Configure(string.Empty, _sandbox, _quarry);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object item in _created)
                Object.DestroyImmediate(item);
            _created.Clear();
        }

        [Test]
        public void IsUnlocked_LevelWithoutRequiredLevel_IsOpenFromTheStart()
        {
            IPlayerProgress data = new JsonPlayerProgress(null);

            Assert.IsTrue(LevelUnlockUseCase.IsUnlocked(_sandbox, data));
            Assert.IsFalse(LevelUnlockUseCase.IsUnlocked(_quarry, data));
        }

        [Test]
        public void IsUnlocked_RequiredLevelCompleted_OpensTheLevel()
        {
            IPlayerProgress data = new JsonPlayerProgress(null);

            data.MarkLevelCompleted("sandbox");

            Assert.IsTrue(LevelUnlockUseCase.IsUnlocked(_quarry, data));
        }

        [Test]
        public void IsUnlocked_LevelUnlockedInTheProgress_IsOpen()
        {
            IPlayerProgress data = new JsonPlayerProgress(null);

            data.UnlockLevel("quarry");

            Assert.IsTrue(LevelUnlockUseCase.IsUnlocked(_quarry, data));
        }

        [Test]
        public void Next_ReturnsTheFollowingLevel_AndNullAfterTheLast()
        {
            Assert.AreSame(_quarry, LevelUnlockUseCase.Next(_catalog, _sandbox));
            Assert.IsNull(LevelUnlockUseCase.Next(_catalog, _quarry));
            Assert.IsNull(LevelUnlockUseCase.Next(null, _sandbox));
        }

        private T Create<T>() where T : ScriptableObject
        {
            T item = ScriptableObject.CreateInstance<T>();
            _created.Add(item);
            return item;
        }
    }
}
