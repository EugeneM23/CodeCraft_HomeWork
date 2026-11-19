using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Game.Voxels.Tests
{
    /// <summary>
    /// Blood soaks into the ground (VoxelDebris.GroundStains): a drop of blood that lands facing up leaves the debris store
    /// as a ground stain; acid, body parts and a stain laid back lie as before. A store of its own over a plane, ticked by hand.
    /// </summary>
    public sealed class VoxelBloodGroundTests
    {
        private const float STEP = 0.02f;
        private static readonly Color Blood = new(0.5f, 0.05f, 0.05f);
        private static readonly Color Acid = new(0.3f, 0.8f, 0.1f);

        private VoxelDebris _debris;
        private GameObject _floor;
        private Material _material;
        private readonly List<VoxelDebris.GroundStain> _stains = new();

        [SetUp]
        public void SetUp()
        {
            _floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            _floor.transform.localScale = Vector3.one * 50f;
            Physics.SyncTransforms();
            _material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { enableInstancing = true };
            _debris = new VoxelDebris(_material, 1024, 256) { GroundStains = true };
            _stains.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            _debris.Dispose();
            Object.Destroy(_floor);
            Object.Destroy(_material);
        }

        private void Run(float seconds)
        {
            for (float t = 0f; t < seconds; t += STEP)
            {
                _debris.Tick(STEP);
                _debris.Complete();
                _debris.TakeGroundStains(_stains, 1000);
            }
        }

        [Test]
        public void BloodDrop_LandingOnTheGround_LeavesTheStoreAsAStain()
        {
            _debris.GooDrop(new Vector3(0f, 1f, 0f), Vector3.up, Blood, 0.06f);
            _debris.Drop(new Vector3(2f, 1f, 0f), Vector3.zero, Blood, 0.05f);
            int blob = _debris.BeginGoreCluster(new Vector3(4f, 1f, 0f), Vector3.zero, 0f);
            for (int i = 0; i < 5; i++)
                _debris.AddCube(blob, new Vector3(4f + i * 0.06f, 1f, 0f), 0.06f, Blood);
            _debris.SetGoo(blob);

            Run(3f);

            Assert.AreEqual(3, _stains.Count, "the goo drop, the drop and the blob soaked in");
            Assert.AreEqual(0, _debris.LyingCount, "no blood lies on the ground");
            Assert.AreEqual(0, _debris.Count);
            foreach (VoxelDebris.GroundStain stain in _stains)
            {
                Assert.That(stain.Normal.y, Is.GreaterThan(0.9f), "facing up");
                Assert.That(stain.Point.y, Is.EqualTo(0f).Within(0.05f), "on the ground");
                Assert.That(stain.Radius, Is.GreaterThan(0f));
            }

            float smallest = float.MaxValue, biggest = 0f;
            foreach (VoxelDebris.GroundStain stain in _stains)
            {
                smallest = Mathf.Min(smallest, stain.Radius);
                biggest = Mathf.Max(biggest, stain.Radius);
            }

            Assert.That(biggest, Is.GreaterThan(smallest * 2f), "a blob makes a bigger stain than a drop");
        }

        [Test]
        public void Acid_BodyParts_AndAStainLaidBack_LieAsBefore()
        {
            _debris.Drop(new Vector3(0f, 1f, 0f), Vector3.zero, Acid, 0.05f);
            int part = _debris.BeginGoreCluster(new Vector3(2f, 1f, 0f), Vector3.zero, 0f);
            for (int i = 0; i < 4; i++)
                _debris.AddCube(part, new Vector3(2f + i * 0.06f, 1f, 0f), 0.06f, Blood);
            _debris.SetGoo(part, true);
            _debris.LayStain(new VoxelDebris.GroundStain
            {
                Point = new Vector3(4f, 0f, 0f), Normal = Vector3.up, Radius = 0.1f, Shade = 1
            });

            Run(3f);

            Assert.AreEqual(0, _stains.Count, "none of them soaks in");
            Assert.AreEqual(1 + 4 + 1, _debris.LyingCount, "the acid drop, the body part's cubes and the tile laid back lie");
        }

        [Test]
        public void ChurnedBlood_StaysRedBrown()
        {
            for (int s = 0; s < VoxelBlood.SHADES; s++)
            for (int l = 0; l < VoxelBlood.LEVELS; l++)
                Assert.IsTrue(VoxelBlood.IsBlood(VoxelBlood.Colour(s, l)), $"shade {s} level {l} still reads as blood");
            Assert.IsFalse(VoxelBlood.IsBlood((Color32)Acid));
            Assert.IsFalse(VoxelBlood.IsBlood((Color32)new Color(0.4f, 0.25f, 0.1f)), "plain earth is not blood");
        }
    }
}
