using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Saving.Tests
{
    /// <summary>Play time on the owner's clock: with TickManually only Tick moves it, frames do not.</summary>
    public class SaveLoadSystemManualTickTests
    {
        private GameObject _object;

        [TearDown]
        public void TearDown()
        {
            if (_object != null)
                Object.DestroyImmediate(_object);
        }

        private SaveLoadSystem Create()
        {
            _object = new GameObject("SaveLoadSystem");
            var system = _object.AddComponent<SaveLoadSystem>();
            system.UseStorage(new InMemorySaveStorage());
            system.Configure(autoSavePeriod: 0f);
            return system;
        }

        [UnityTest]
        public IEnumerator SaveLoadSystem_Frames_AddPlayTime()
        {
            SaveLoadSystem system = Create();

            for (int i = 0; i < 3; i++)
                yield return null;

            Assert.Greater(system.PlayTime, 0);
        }

        [UnityTest]
        public IEnumerator SaveLoadSystem_TickManually_FramesDoNotAddPlayTime()
        {
            SaveLoadSystem system = Create();
            system.TickManually = true;

            for (int i = 0; i < 3; i++)
                yield return null;
            Assert.AreEqual(0, system.PlayTime);

            system.Tick(1.5f);
            Assert.AreEqual(1.5, system.PlayTime, 1e-6);
        }
    }
}
