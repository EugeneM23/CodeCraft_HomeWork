using System;
using Loading;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Tests
{
    /// <summary>The trees LevelLoader builds, without running them (PlayMode LevelLoaderTests load real scenes).</summary>
    public class LevelLoaderTreeTests
    {
        private LevelConfig _level;
        private readonly LevelLoader _loader = new(_ => true);

        [SetUp]
        public void SetUp() => _level = ScriptableObject.CreateInstance<LevelConfig>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_level);

        [Test]
        public void Load_WeighsOnePerScene()
        {
            _level.Configure("b", "", 1, "Assets/Test/B_Geo.unity", "Assets/Test/B.unity", "Assets/Test/B_Fx.unity");

            LoadingOperation tree = _loader.Load(_level);

            Assert.AreEqual(3f, tree.Weight);
        }

        [Test]
        public void Load_ALevelWithoutScenes_Throws()
        {
            _level.Configure("empty", "", 0);
            Assert.Throws<ArgumentException>(() => _loader.Load(_level));
        }

        [Test]
        public void Load_AnActiveIndexOutOfTheScenes_Throws()
        {
            _level.Configure("a", "", 2, "Assets/Test/A.unity");
            Assert.Throws<ArgumentException>(() => _loader.Load(_level));
        }

        [Test]
        public void Load_ASceneThatCannotLoad_ThrowsBeforeAnythingLoads()
        {
            var loader = new LevelLoader(path => path != "Assets/Test/B.unity");
            _level.Configure("b", "", 0, "Assets/Test/B_Geo.unity", "Assets/Test/B.unity");

            Assert.Throws<ArgumentException>(() => loader.Load(_level));
        }

        [Test]
        public void Load_NoLevel_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => _loader.Load(null));
        }
    }
}
