using NUnit.Framework;
using Saving.Objects;
using UnityEngine;

namespace Saving.Tests
{
    /// <summary>Which serializer a component type gets: own, then built-in, then [Save] fields; discovery on its own.</summary>
    public class ComponentSerializerCatalogTests
    {
        private static ComponentSerializerCatalog Catalog(IComponentSerializer[] own, IComponentSerializer[] builtIn) =>
            new(own, builtIn);

        [Test]
        public void Catalog_OwnSerializer_BeatsBuiltIn()
        {
            var own = new TaggedSerializer(typeof(Transform), "own");
            ComponentSerializerCatalog catalog = Catalog(new IComponentSerializer[] { own },
                new IComponentSerializer[] { new TransformSerializer() });

            Assert.AreSame(own, catalog.For(typeof(Transform)));
            Assert.AreEqual(SerializerKind.Own, catalog.KindOf(typeof(Transform)));
        }

        [Test]
        public void Catalog_BuiltIn_BeatsSaveFields()
        {
            var builtIn = new TaggedSerializer(typeof(FieldsProbe), "built-in");
            ComponentSerializerCatalog catalog = Catalog(new IComponentSerializer[0], new IComponentSerializer[] { builtIn });

            Assert.AreSame(builtIn, catalog.For(typeof(FieldsProbe)));
            Assert.AreEqual(SerializerKind.BuiltIn, catalog.KindOf(typeof(FieldsProbe)));
        }

        [Test]
        public void Catalog_OnlySaveFields_UsesFields()
        {
            ComponentSerializerCatalog catalog = Catalog(new IComponentSerializer[0], new IComponentSerializer[0]);

            Assert.AreEqual(typeof(FieldsProbe), catalog.For(typeof(FieldsProbe)).ComponentType);
            Assert.AreEqual(SerializerKind.Fields, catalog.KindOf(typeof(FieldsProbe)));
        }

        [Test]
        public void Catalog_NothingToSave_None()
        {
            ComponentSerializerCatalog catalog = Catalog(new IComponentSerializer[0], new IComponentSerializer[0]);

            Assert.IsNull(catalog.For(typeof(PlainProbe)));
            Assert.AreEqual(SerializerKind.None, catalog.KindOf(typeof(PlainProbe)));
        }

        [Test]
        public void Catalog_BaseTypeSerializer_ServesSubclass()
        {
            var forBase = new TaggedSerializer(typeof(FieldsProbeBase), "base");
            ComponentSerializerCatalog catalog = Catalog(new IComponentSerializer[] { forBase }, new IComponentSerializer[0]);

            Assert.AreSame(forBase, catalog.For(typeof(FieldsProbe)));
        }

        [Test]
        public void Catalog_Default_FindsOwnAndBuiltInSerializers()
        {
            Assert.IsInstanceOf<ProbeSerializer>(ComponentSerializerCatalog.Default.For(typeof(SerializerProbe)));
            Assert.IsInstanceOf<TransformSerializer>(ComponentSerializerCatalog.Default.For(typeof(Transform)));
        }

        [Test]
        public void Catalog_VersionOf_ByTypeNameOrOne()
        {
            Assert.AreEqual(2, ComponentSerializerCatalog.Default.VersionOf(nameof(SerializerProbe)));
            Assert.AreEqual(1, ComponentSerializerCatalog.Default.VersionOf("NoSuchComponent"));
        }
    }
}
