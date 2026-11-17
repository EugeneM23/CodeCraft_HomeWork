using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Saving.Objects;
using UnityEngine;
using UnityEngine.TestTools;

namespace Saving.Tests
{
    /// <summary>Saved references read back as another type, or pointing at a component that is gone.</summary>
    public class SaveReferencesEdgeTests
    {
        private TempScene _scene;
        private SaveSystemSettings _settings;
        private Saveable _owner;

        [SetUp]
        public void SetUp()
        {
            _scene = new TempScene();
            _settings = ScriptableObject.CreateInstance<SaveSystemSettings>();
            SaveReferences.Settings = _settings;
            _owner = new GameObject("Owner").AddComponent<Saveable>();
            _owner.SetId(System.Guid.NewGuid().ToString("N"));
            SaveRegistry.Register(_owner);
        }

        [TearDown]
        public void TearDown()
        {
            SaveRegistry.Unregister(_owner);
            SaveReferences.Settings = null;
            Object.DestroyImmediate(_settings);
            _scene.Dispose();
        }

        [Test]
        public void Ref_ComponentReadAsGameObject_GivesItsObject()
        {
            JToken token = SaveReferences.Write(_owner.transform);

            Assert.AreEqual("Transform", (string)token["component"]);
            Assert.AreSame(_owner.gameObject, SaveReferences.Read(token, typeof(GameObject)));
        }

        [Test]
        public void Ref_ComponentGoneSinceSave_NullWithWarning()
        {
            var token = new JObject { ["id"] = _owner.Id, ["component"] = "BoxCollider2D" };

            LogAssert.Expect(LogType.Warning, new Regex("was not found"));
            Assert.IsNull(SaveReferences.Read(token, typeof(BoxCollider2D)));
        }

        [Test]
        public void Ref_ReadAsUnrelatedType_NullWithWarning()
        {
            JToken token = SaveReferences.Write(_owner.gameObject);

            LogAssert.Expect(LogType.Warning, new Regex("was not found"));
            Assert.IsNull(SaveReferences.Read(token, typeof(Sprite)));
        }
    }
}
