using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Voxels.Tests
{
    public sealed class VoxelGibsPlayModeTests
    {
        private readonly List<GameObject> _created = new();
        private SkinnedMeshRenderer _skin;
        private Transform _upper;
        private IEntity _character;

        [SetUp]
        public void SetUp()
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.transform.localScale = Vector3.one * 4f;
            _created.Add(floor);
            Physics.SyncTransforms();

            _skin = SkinnedColumn.Create(new Vector3(0f, 0.05f, 0f), out Transform root, out _, out _upper);
            _created.Add(root.gameObject);

            var config = new VoxelGibsConfig
            {
                parts = new[]
                {
                    new VoxelGibsConfig.PartSpec { name = "Bottom", bones = new[] { "Lower" } },
                    new VoxelGibsConfig.PartSpec { name = "Top", bones = new[] { "Upper" } }
                },
                seams = new[] { new VoxelGibsConfig.SeamSpec { a = 0, b = 1, hold = 0f } },
                spineFrom = "Lower",
                spineTo = "Upper",
                leftMarker = ""
            };

            _character = new Entity();
            _character.AddDamageTakenEvent(new Event<TakeDamageArgs>());
            _character.AddDeathTakenEvent(new Event<TakeDamageArgs>());
            var rig = VoxelGibsRig.Resolve(_skin, root, new Renderer[] { _skin }, config);
            new VoxelGibsOnDeathBehaviour(rig, config).Init(_character);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _created)
                if (go != null)
                    Object.Destroy(go);
            _created.Clear();
            foreach (VoxelBody body in Object.FindObjectsByType<VoxelBody>(FindObjectsSortMode.None))
                Object.Destroy(body.gameObject);
        }

        private void KilledFrom(Vector3 position)
        {
            var go = new GameObject("Killer");
            _created.Add(go);
            go.transform.position = position;

            IEntity killer = new Entity();
            killer.AddTransform(go.transform);
            killer.AddDamage(new ReactiveInt(50));
            _character.GetDamageTakenEvent().Invoke(new TakeDamageArgs(killer));
            _character.GetDeathTakenEvent().Invoke(new TakeDamageArgs(_character));
        }

        private static VoxelBody[] Flesh() =>
            Object.FindObjectsByType<VoxelBody>(FindObjectsSortMode.None).Where(b => b.name.Contains(VoxelGibs.NAME)).ToArray();

        private static int Drops() => VoxelWorld.Instance.Debris.Count + VoxelWorld.Instance.Debris.LyingCount;

        [UnityTest]
        public IEnumerator Death_HidesTheSkin_AndThrowsFleshAwayFromTheKiller()
        {
            int before = Drops();
            KilledFrom(new Vector3(0f, 0.5f, -5f));

            Assert.IsFalse(_skin.enabled);
            Assert.AreEqual(2, Flesh().Length, "the seam never holds: two pieces");
            Assert.That(Drops(), Is.GreaterThan(before + 40), "blood from the torn seam and the wound");

            yield return null;
            yield return new WaitForFixedUpdate();
            foreach (VoxelBody piece in Flesh())
            {
                Rigidbody rigidbody = piece.GetComponent<Rigidbody>();
                Assert.IsNotNull(rigidbody);
                Assert.IsFalse(rigidbody.isKinematic);
                Assert.That(rigidbody.linearVelocity.z, Is.GreaterThan(0f), "away from the killer behind it");
            }
        }

        [Test]
        public void APoseThatCannotBeVoxelized_LeavesTheBodyAsItIs()
        {
            // The top flung 9 m off: past the largest grid the voxelizer takes.
            _upper.position += Vector3.right * 9f;
            LogAssert.Expect(LogType.Warning, new Regex("not voxelized"));
            LogAssert.Expect(LogType.Warning, new Regex(@"\[VOXEL GIBS\].*stays as it is"));
            int before = Flesh().Length; // the last test's flesh goes only at the end of the frame

            KilledFrom(new Vector3(0f, 0.5f, -5f));

            Assert.IsTrue(_skin.enabled, "no flesh to take its place: the body stays seen");
            Assert.AreEqual(before, Flesh().Length, "and no flesh");
        }

        [Test]
        public void Dispose_WithoutInit_DoesNotThrow()
        {
            var behaviour = new VoxelGibsOnDeathBehaviour(null, new VoxelGibsConfig());
            Assert.DoesNotThrow(() => behaviour.Dispose(new Entity()));
        }

        [UnityTest]
        public IEnumerator Flesh_SettlesAndStays()
        {
            KilledFrom(new Vector3(0f, 0.5f, -5f));
            yield return new WaitForSeconds(8f);

            VoxelBody[] flesh = Flesh();
            Assert.That(flesh.Length, Is.GreaterThanOrEqualTo(2));
            Assert.That(flesh.Count(b => !b.IsLoose), Is.GreaterThan(0), "pieces at rest stop simulating");
        }
    }
}
