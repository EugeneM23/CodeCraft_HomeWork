using System.Collections;
using Game.GameLoop.Adapters;
using Game.Voxels;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests
{
    public sealed class VoxelTickBridgeTests
    {
        private Ticking.GameLoop _loop;
        private VoxelTickBridge _bridge;
        private GameObject _source;

        [SetUp]
        public void SetUp() => _loop = new Ticking.GameLoop();

        [TearDown]
        public void TearDown()
        {
            if (_bridge != null)
            {
                _loop.Remove(_bridge);
                _bridge.Dispose();
                _bridge = null;
            }

            if (_source != null)
                Object.Destroy(_source); // the cloud is its child
        }

        [Test]
        public void TheBridge_DrivesTheHub_WithTheLoopsTime_AndLetsGoOnDispose()
        {
            _loop.Update(3f);
            _bridge = new VoxelTickBridge(_loop);
            Assert.IsTrue(VoxelTicks.Driven);
            Assert.AreEqual(3f, VoxelTicks.Time);

            var counter = new UpdateCounter();
            VoxelTicks.Add(counter);
            _loop.Add(_bridge);
            try
            {
                _loop.Update(0.5f);
                Assert.AreEqual(1, counter.Ticks);
                Assert.AreEqual(0.5f, counter.LastDelta);
                Assert.AreEqual(3.5f, VoxelTicks.Time);

                _loop.Pause(this);
                Assert.IsTrue(VoxelTicks.Paused, "renderers draw from their own LateUpdate while paused");
                _loop.Update(0.5f);
                Assert.AreEqual(1, counter.Ticks);
                Assert.AreEqual(3.5f, VoxelTicks.Time);
                _loop.Resume(this);
                Assert.IsFalse(VoxelTicks.Paused);

                _loop.Remove(_bridge);
                _bridge.Dispose();
                _bridge = null;
                Assert.IsFalse(VoxelTicks.Driven);
                Assert.AreEqual(Time.time, VoxelTicks.Time);
            }
            finally
            {
                VoxelTicks.Remove(counter);
            }
        }

        [UnityTest]
        public IEnumerator Driven_AVoxelComponentWaitsForTheLoop_AndTicksFromIt()
        {
            _bridge = new VoxelTickBridge(_loop);
            _loop.Add(_bridge);

            _source = new GameObject("VoxelTickBridgeTests");
            var source = _source.AddComponent<VoxelSmokeSource>();
            source.Set(1f, 5f, 1f, 0f);

            // Unity's frames alone: the source's own Update stands idle under the bridge
            yield return null;
            yield return null;
            Assert.IsNull(source.Cloud);

            _loop.Update(0.016f);
            Assert.IsNotNull(source.Cloud, "the loop's tick did not reach the voxel component");
        }

        private sealed class UpdateCounter : IVoxelUpdate
        {
            public int Ticks { get; private set; }
            public float LastDelta { get; private set; }

            public void VoxelUpdate(float deltaTime)
            {
                Ticks++;
                LastDelta = deltaTime;
            }
        }
    }
}
