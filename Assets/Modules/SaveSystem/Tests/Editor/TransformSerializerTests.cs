using NUnit.Framework;
using Saving.Objects;
using UnityEngine;

namespace Saving.Tests
{
    /// <summary>The built-in Transform serializer keeps the local pose.</summary>
    public class TransformSerializerTests
    {
        private TempScene _scene;

        [SetUp]
        public void SetUp() => _scene = new TempScene();

        [TearDown]
        public void TearDown() => _scene.Dispose();

        [Test]
        public void TransformSerializer_LocalPose_RoundTrips()
        {
            Transform transform = new GameObject("Moved").transform;
            transform.localPosition = new Vector3(1f, 2f, 3f);
            transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            transform.localScale = new Vector3(2f, 2f, 1f);
            IComponentSerializer serializer = new TransformSerializer();
            var data = serializer.Capture(transform);

            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
            serializer.Restore(transform, data);

            Assert.AreEqual(new Vector3(1f, 2f, 3f), transform.localPosition);
            Assert.AreEqual(Quaternion.Euler(0f, 0f, 45f), transform.localRotation);
            Assert.AreEqual(new Vector3(2f, 2f, 1f), transform.localScale);
            Assert.IsNotNull(data["localPosition"]);
        }
    }
}
