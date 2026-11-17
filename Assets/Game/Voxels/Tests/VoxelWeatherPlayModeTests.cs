using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Voxels.Tests
{
    /// <summary>Voxel smoke and rain on a bare floor, far from anything else in the test scene.</summary>
    public sealed class VoxelWeatherPlayModeTests
    {
        private static readonly Vector3 Site = new(500f, 0f, 500f);
        private readonly List<GameObject> _spawned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _spawned)
                if (go != null)
                    Object.Destroy(go);
            _spawned.Clear();

            foreach (VoxelSmoke smoke in Object.FindObjectsByType<VoxelSmoke>(FindObjectsSortMode.None))
                Object.Destroy(smoke.gameObject);
        }

        [UnityTest]
        public IEnumerator Smoke_FillsOutRoundItsSource()
        {
            Floor();
            VoxelSmoke smoke = Track(VoxelSmoke.Deploy(Site + Vector3.up * 0.1f));
            yield return WaitSeconds(smoke.SpreadTime + 0.8f);

            Assert.That(smoke.FilledCells, Is.GreaterThan(1000));
            Assert.That(smoke.DrawnCubes, Is.GreaterThan(500));
            Assert.That(VoxelSmoke.DensityAt(Site + new Vector3(0f, 1f, 0f)), Is.GreaterThan(0.8f));
            Assert.That(VoxelSmoke.DensityAt(Site + new Vector3(2f, 0.6f, 0f)), Is.GreaterThan(0.5f));
            Assert.That(VoxelSmoke.DensityAt(Site + new Vector3(6.5f, 1f, 0f)), Is.EqualTo(0f));
            // Never under the floor.
            Assert.That(VoxelSmoke.DensityAt(Site + new Vector3(0f, -0.4f, 0f)), Is.EqualTo(0f));
        }

        [UnityTest]
        public IEnumerator Smoke_DoesNotPassThroughAWall()
        {
            Floor();
            // A wall 1.5 m east of the source, wider and higher than the cloud.
            Box(Site + new Vector3(1.5f, 2f, 0f), new Vector3(0.3f, 4f, 16f));
            VoxelSmoke smoke = Track(VoxelSmoke.Deploy(Site + Vector3.up * 0.1f));
            yield return WaitSeconds(smoke.SpreadTime + 0.8f);

            Assert.That(VoxelSmoke.DensityAt(Site + new Vector3(-2.2f, 0.6f, 0f)), Is.GreaterThan(0.5f));
            Assert.That(VoxelSmoke.DensityAt(Site + new Vector3(2.2f, 0.6f, 0f)), Is.EqualTo(0f));
            // Kept in on one side, it runs further the other way than in the open.
            Assert.That(VoxelSmoke.DensityAt(Site + new Vector3(-3.3f, 0.5f, 0f)), Is.GreaterThan(0.2f));
        }

        [UnityTest]
        public IEnumerator Smoke_ShotOpensATunnelThatClosesAgain()
        {
            Floor();
            VoxelSmoke smoke = Track(VoxelSmoke.Deploy(Site + Vector3.up * 0.1f));
            yield return WaitSeconds(smoke.SpreadTime + 1f);

            Vector3 inside = Site + new Vector3(0f, 1f, 0f);
            Vector3 aside = Site + new Vector3(0f, 1f, 1.2f);
            Assert.That(VoxelSmoke.DensityAt(inside), Is.GreaterThan(0.8f));

            VoxelSmoke.Cut(Site + new Vector3(-8f, 1f, 0f), Site + new Vector3(8f, 1f, 0f));
            Assert.That(VoxelSmoke.DensityAt(inside), Is.LessThan(0.1f));
            Assert.That(VoxelSmoke.DensityAt(aside), Is.GreaterThan(0.8f));

            yield return WaitSeconds(2f);
            Assert.That(VoxelSmoke.DensityAt(inside), Is.GreaterThan(0.8f));
        }

        [UnityTest]
        public IEnumerator Smoke_BlastClearsASphere()
        {
            Floor();
            VoxelSmoke smoke = Track(VoxelSmoke.Deploy(Site + Vector3.up * 0.1f));
            yield return WaitSeconds(smoke.SpreadTime + 1f);

            VoxelSmoke.Blast(Site + new Vector3(0f, 0.5f, 0f), 2f);
            yield return null;
            Assert.That(VoxelSmoke.DensityAt(Site + new Vector3(1f, 0.8f, 0.5f)), Is.LessThan(0.1f));
            Assert.That(VoxelSmoke.DensityAt(Site + new Vector3(0f, 0.8f, -1.4f)), Is.LessThan(0.1f));
        }

        [UnityTest]
        public IEnumerator Smoke_ThinsAwayAndIsGoneAfterItsLifetime()
        {
            Floor();
            VoxelSmoke smoke = VoxelSmoke.Deploy(Site + Vector3.up * 0.1f, 3.2f, 5f);
            yield return WaitSeconds(5.3f);

            Assert.That(smoke == null, "the cloud is destroyed");
            Assert.That(VoxelSmoke.Active.Count, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator Rain_FallsAndSplashesOnTheGround()
        {
            Floor(new Vector3(200f, 0.2f, 200f));
            var camera = new GameObject("RainCamera") { tag = "MainCamera" }.AddComponent<Camera>();
            camera.transform.SetPositionAndRotation(Site + new Vector3(0f, 12f, -12f), Quaternion.Euler(45f, 0f, 0f));
            _spawned.Add(camera.gameObject);

            var rain = new GameObject("Rain").AddComponent<VoxelRain>();
            _spawned.Add(rain.gameObject);
            yield return WaitSeconds(2.5f);

            Assert.That(rain.Splashes, Is.GreaterThan(50));
        }

        private static IEnumerator WaitSeconds(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end)
                yield return null;
        }

        private VoxelSmoke Track(VoxelSmoke smoke)
        {
            _spawned.Add(smoke.gameObject);
            return smoke;
        }

        private void Floor(Vector3? size = null) => Box(Site + Vector3.down * 0.1f, size ?? new Vector3(30f, 0.2f, 30f));

        private void Box(Vector3 centre, Vector3 size)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.transform.position = centre;
            box.transform.localScale = size;
            _spawned.Add(box);
        }
    }
}
