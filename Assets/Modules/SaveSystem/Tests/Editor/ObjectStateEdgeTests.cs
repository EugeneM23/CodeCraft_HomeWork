using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Saving.Objects;
using UnityEngine;
using UnityEngine.TestTools;

namespace Saving.Tests
{
    /// <summary>
    /// One object's state at its edges: unchecked and outdated component data, an empty state, options switched off,
    /// and broken rows of the asset table.
    /// </summary>
    public class ObjectStateEdgeTests
    {
        private TempScene _scene;

        [SetUp]
        public void SetUp() => _scene = new TempScene();

        [TearDown]
        public void TearDown() => _scene.Dispose();

        private static Saveable MakeSaveable<T>(out T component) where T : Component
        {
            var root = new GameObject("Root");
            var saveable = root.AddComponent<Saveable>();
            saveable.SetId(System.Guid.NewGuid().ToString("N"));
            component = root.AddComponent<T>();
            return saveable;
        }

        private static ComponentSerializerCatalog Catalog => ComponentSerializerCatalog.Default;

        [Test]
        public void Restore_ComponentUncheckedSinceSave_KeepsItsValue()
        {
            Saveable saveable = MakeSaveable(out FieldsProbe probe);
            probe.Hp = 5;
            ObjectData data = ObjectState.Capture(saveable, Catalog);
            probe.Hp = 1;
            saveable.SetSaved("FieldsProbe", false);

            ObjectState.RestoreComponents(saveable, data, Catalog);

            Assert.AreEqual(1, probe.Hp);
        }

        [Test]
        public void Restore_ComponentVersionZero_SkippedWithError()
        {
            Saveable saveable = MakeSaveable(out FieldsProbe probe);
            probe.Hp = 5;
            ObjectData data = ObjectState.Capture(saveable, Catalog);
            data.Components["FieldsProbe"].V = 0;
            probe.Hp = 1;

            LogAssert.Expect(LogType.Error, new Regex("Loading FieldsProbe of 'Root' failed.*invalid version 0"));
            ObjectState.RestoreComponents(saveable, data, Catalog);

            Assert.AreEqual(1, probe.Hp);
        }

        [Test]
        public void Restore_OlderComponentVersion_UpgradedThenApplied()
        {
            Saveable saveable = MakeSaveable(out SerializerProbe probe);
            var data = new ObjectData();
            data.Components["SerializerProbe"] = new ComponentData
            {
                V = 1,
                Data = JObject.Parse("{ \"spd\": 3, \"mode\": \"fast\" }")
            };

            ObjectState.RestoreComponents(saveable, data, Catalog);

            Assert.AreEqual(3f, probe.Speed);
            Assert.AreEqual("fast", probe.Mode);
        }

        [Test]
        public void Saveable_RestoreEmptyState_ChangesNothing()
        {
            Saveable saveable = MakeSaveable(out FieldsProbe probe);
            probe.Hp = 4;

            saveable.RestoreState(JValue.CreateNull());
            saveable.RestoreState(null);

            Assert.IsTrue(saveable.gameObject.activeSelf);
            Assert.AreEqual(4, probe.Hp);
        }

        [Test]
        public void Saveable_OptionOff_NoLongerOn()
        {
            Saveable saveable = MakeSaveable(out FieldsProbe _);
            saveable.SetOption("FieldsProbe", "extra", true);

            saveable.SetOption("FieldsProbe", "extra", false);
            saveable.SetOption("FieldsProbe", "extra", false);

            Assert.IsFalse(saveable.IsOptionOn("FieldsProbe", "extra"));
            CollectionAssert.IsEmpty(saveable.OptionsOf("FieldsProbe"));
        }

        [Test]
        public void Settings_BrokenAssetRows_Skipped()
        {
            var settings = ScriptableObject.CreateInstance<SaveSystemSettings>();
            var asset = ScriptableObject.CreateInstance<SaveSystemSettings>();
            try
            {
                settings.SetAssetCatalog(new[]
                {
                    null,
                    new SaveSystemSettings.AssetEntry { Key = "", Asset = asset },
                    new SaveSystemSettings.AssetEntry { Key = "empty", Asset = null },
                    new SaveSystemSettings.AssetEntry { Key = "kept", Asset = asset }
                });

                Assert.IsFalse(settings.TryGetAsset("empty", out _));
                Assert.IsFalse(settings.TryGetAsset("", out _));
                Assert.IsTrue(settings.TryGetAsset("kept", out Object found));
                Assert.AreSame(asset, found);
                Assert.IsTrue(settings.TryGetAssetKey(asset, out string key));
                Assert.AreEqual("kept", key);
            }
            finally
            {
                Object.DestroyImmediate(settings);
                Object.DestroyImmediate(asset);
            }
        }
    }
}
