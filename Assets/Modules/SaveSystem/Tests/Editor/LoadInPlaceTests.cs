using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Saving.Tests
{
    /// <summary>LoadInPlace: a load during which the objects layer keeps the scenes; outside it, Load as usual.</summary>
    public class LoadInPlaceTests
    {
        /// <summary>Notes what the service told the layer while checking the file and restoring it.</summary>
        private sealed class SceneProbe : ISaveExtension
        {
            public SaveLoadService Service;
            public bool? KeptSceneOnCheck;
            public bool? KeptSceneOnBegin;

            public void CaptureInto(IDictionary<string, SaveSection> sections) =>
                sections["$probe"] = new SaveSection { V = 1, Data = new JValue(1) };

            public LoadOutcome Check(IReadOnlyDictionary<string, SaveSection> sections, out string problem)
            {
                KeptSceneOnCheck = Service.KeepsScene;
                problem = null;
                return LoadOutcome.Loaded;
            }

            public void BeginLoad(Action finished)
            {
                KeptSceneOnBegin = Service.KeepsScene;
                finished();
            }

            public void Dispose()
            {
            }
        }

        private static (SaveLoadService Service, SceneProbe Probe) CreateSaved()
        {
            var storage = new InMemorySaveStorage();
            var writer = new SaveLoadService(storage, "1.0");
            writer.Attach(new SceneProbe { Service = writer });
            writer.Save();

            var service = new SaveLoadService(storage, "1.0");
            var probe = new SceneProbe { Service = service };
            service.Attach(probe);
            return (service, probe);
        }

        [Test]
        public void LoadInPlace_LayerKeepsSceneWhileCheckingAndRestoring()
        {
            (SaveLoadService service, SceneProbe probe) = CreateSaved();

            Assert.AreEqual(LoadOutcome.Loaded, service.LoadInPlace());

            Assert.AreEqual(true, probe.KeptSceneOnCheck);
            Assert.AreEqual(true, probe.KeptSceneOnBegin);
            Assert.IsFalse(service.KeepsScene, "only during that load");
        }

        [Test]
        public void Load_LayerDoesNotKeepScene()
        {
            (SaveLoadService service, SceneProbe probe) = CreateSaved();

            Assert.AreEqual(LoadOutcome.Loaded, service.Load());

            Assert.AreEqual(false, probe.KeptSceneOnCheck);
            Assert.AreEqual(false, probe.KeptSceneOnBegin);
        }

        [Test]
        public void LoadInPlace_EmptySlot_EmptyAndFlagOff()
        {
            var service = new SaveLoadService(new InMemorySaveStorage(), "1.0");

            Assert.AreEqual(LoadOutcome.Empty, service.LoadInPlace());
            Assert.IsFalse(service.KeepsScene);
        }

        [Test]
        public void LoadInPlace_LoadedSubscriber_SeesAnOrdinaryLoad()
        {
            (SaveLoadService service, _) = CreateSaved();
            bool? keptSceneInLoaded = null;
            service.Loaded += _ => keptSceneInLoaded = service.KeepsScene;

            Assert.AreEqual(LoadOutcome.Loaded, service.LoadInPlace());

            Assert.AreEqual(false, keptSceneInLoaded, "a subscriber that loads again must get an ordinary load");
        }
    }
}
