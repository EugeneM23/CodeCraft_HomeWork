using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Voxels.Tests
{
    public sealed class VoxelGibsPlayModeTests
    {
        private readonly List<GameObject> _created = new();
        private SkinnedMeshRenderer _skin;
        private Transform _upper;
        private Animator _animator;
        private VoxelGibsConfig _config;
        private VoxelGibsRig _rig;
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
            // What poses a real body; the column has no clips, it only shows the death holds it still.
            _animator = root.gameObject.AddComponent<Animator>();

            _config = Config();
            _character = new Entity();
            _character.AddDamageTakenEvent(new Event<TakeDamageArgs>());
            _character.AddDeathTakenEvent(new Event<TakeDamageArgs>());
            _rig = VoxelGibsRig.Resolve(_skin, root, new Renderer[] { _skin }, _config);
            new VoxelGibsOnDeathBehaviour(_rig, _config).Init(_character);
        }

        private static VoxelGibsConfig Config() => new()
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

        [TearDown]
        public void TearDown()
        {
            VoxelGibs.Flush();
            VoxelGibs.DeathsPerFrame = 2;
            foreach (GameObject go in _created)
                if (go != null)
                    Object.Destroy(go);
            _created.Clear();
            foreach (VoxelBody body in Object.FindObjectsByType<VoxelBody>(FindObjectsInactive.Include, FindObjectsSortMode.None))
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

        private static VoxelBody[] Flesh(bool inactiveToo = false) =>
            Object.FindObjectsByType<VoxelBody>(inactiveToo ? FindObjectsInactive.Include : FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(b => b.name.Contains(VoxelGibs.NAME)).ToArray();

        private static int Drops() => VoxelWorld.Instance.Debris.Count + VoxelWorld.Instance.Debris.LyingCount;

        [UnityTest]
        public IEnumerator Death_HidesTheSkin_AndThrowsFleshAwayFromTheKiller()
        {
            int before = Drops();
            KilledFrom(new Vector3(0f, 0.5f, -5f));
            yield return null;
            yield return null;

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

        [UnityTest]
        public IEnumerator Death_TheSkinHoldsItsPose_UntilTheFleshIsBuilt_ThenTheyTradePlaces()
        {
            yield return null; // the last test's flesh goes at the end of its frame
            KilledFrom(new Vector3(0f, 0.5f, -5f));

            Assert.IsTrue(_skin.enabled, "the death frame: still the skin");
            Assert.IsFalse(_animator.enabled, "held in the pose that was voxelized");
            Assert.AreEqual(0, Flesh(true).Length, "no flesh yet, only its jobs");

            yield return null;
            Assert.IsTrue(_skin.enabled, "a frame on: still the skin");
            Assert.AreEqual(0, Flesh().Length, "the flesh is made but not shown");
            Assert.AreEqual(2, Flesh(true).Length);

            yield return null;
            Assert.IsFalse(_skin.enabled, "two frames on: the flesh in its place");
            Assert.AreEqual(2, Flesh().Length);
            Assert.AreEqual(0, VoxelGibs.Pending);
        }

        [UnityTest]
        public IEnumerator DeathsOfOneFrame_TakeTurns_WithinTheFrameBudget()
        {
            yield return null; // the last test's flesh goes at the end of its frame
            SkinnedMeshRenderer second = SkinnedColumn.Create(new Vector3(2f, 0.05f, 0f), out Transform root, out _, out _);
            _created.Add(root.gameObject);
            VoxelGibsRig rig = VoxelGibsRig.Resolve(second, root, new Renderer[] { second }, _config);
            VoxelGibs.DeathsPerFrame = 1;
            var hit = new KillingHit(new Vector3(0f, 0.5f, -5f), 50f, false, 0f);

            Assert.IsTrue(VoxelGibs.Burst(_rig, _config, hit));
            Assert.IsTrue(VoxelGibs.Burst(rig, _config, hit));
            Assert.AreEqual(2, VoxelGibs.Pending);

            yield return null; // the first built
            Assert.AreEqual(2, VoxelGibs.Pending);
            Assert.IsTrue(_skin.enabled && second.enabled);
            yield return null; // the first shown, the second built
            Assert.AreEqual(1, VoxelGibs.Pending);
            Assert.IsFalse(_skin.enabled);
            Assert.IsTrue(second.enabled, "its turn has not come: it still stands");
            yield return null; // the second shown
            Assert.AreEqual(0, VoxelGibs.Pending);
            Assert.IsFalse(second.enabled);
            Assert.AreEqual(4, Flesh().Length);
        }

        [Test]
        public void TheDeathFrame_AfterTheWarmUp_AllocatesNothing()
        {
            VoxelGibs.WarmUp(_rig, _config);
            var hit = new KillingHit(new Vector3(0f, 0.5f, -5f), 50f, false, 0f);
            // Once through, so the test's own first calls (the profiler of the GC count, the marker) are paid.
            Assert.IsTrue(VoxelGibs.Burst(_rig, _config, hit));
            VoxelGibs.Flush();
            _skin.enabled = true;

            long before = System.GC.GetAllocatedBytesForCurrentThread();
            bool burst = VoxelGibs.Burst(_rig, _config, hit);
            long allocated = System.GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.IsTrue(burst);
            Assert.AreEqual(0, allocated, "the death itself: the pose read and the voxels scheduled, no garbage");
        }

        [UnityTest]
        public IEnumerator APooledDeath_AfterTheWarmUp_AllocatesNothing()
        {
            // A level whose pool keeps one corpse: the second death takes the first one's bodies.
            VoxelCorpses.Clear();
            VoxelGibs.Corpses = 1;
            VoxelGibs.BodiesPerCorpse = 3;
            try
            {
                VoxelGibs.Prepare(_rig, _config);
                for (int frame = 0; VoxelCorpses.ToPrime > 0 && frame < 60; frame++)
                    yield return null;
                Assert.AreEqual(0, VoxelCorpses.ToPrime, "the level's bodies are made");

                var hit = new KillingHit(new Vector3(0f, 0.5f, -5f), 50f, false, 0f);
                // Twice through: the code compiled, the lists grown, the corpse's bodies used once (the second takes the
                // first one's), and the debris' lying cell under the drops made (VoxelDebris bakes a mesh per 8 m cell
                // the first time drops lie in it: its own one-off garbage, not the death's).
                Die(hit);
                yield return null;
                Die(hit);
                yield return null;

                Assert.That(() => Die(hit),
                    UnityEngine.TestTools.Constraints.ConstraintExtensions.AllocatingGCMemory(UnityEngine.TestTools.Constraints.Is.Not),
                    "the whole death: the pose read, the pieces built from the pool, shown, split, bled and built by the world");
                Assert.AreEqual(1, VoxelCorpses.CorpseCount, "the first corpse went to make the second");
                Assert.AreEqual(2, Flesh().Length);
            }
            finally
            {
                VoxelGibs.Corpses = 20;
                VoxelGibs.BodiesPerCorpse = 7;
                VoxelCorpses.Clear();
            }
        }

        /// <summary>A death played through in one go, the same voxels each time; the skin shown again for the next.</summary>
        private void Die(in KillingHit hit)
        {
            _skin.enabled = true;
            Random.InitState(5);
            VoxelGibs.Burst(_rig, _config, hit);
            VoxelGibs.Flush();
            VoxelWorld.Instance.VoxelLateUpdate(0f);
        }

        [UnityTest]
        public IEnumerator EveryLevel_HasItsCorpseBodiesMade()
        {
            Scene test = SceneManager.GetActiveScene();
            Assert.IsTrue(VoxelCorpses.IsPreparedFor(test), "the first body that came in (SetUp) made this level ready");

            Scene level = SceneManager.CreateScene("GibsLevel");
            SceneManager.SetActiveScene(level);
            IEntity other = new Entity();
            other.AddDamageTakenEvent(new Event<TakeDamageArgs>());
            other.AddDeathTakenEvent(new Event<TakeDamageArgs>());
            new VoxelGibsOnDeathBehaviour(_rig, _config).Init(other);

            Assert.IsTrue(VoxelCorpses.IsPreparedFor(level), "a new level: its own bodies");
            Assert.AreEqual(VoxelGibs.Corpses * VoxelGibs.BodiesPerCorpse, VoxelCorpses.ToPrime);
            yield return null;
            yield return null;
            Assert.That(VoxelCorpses.ToPrime, Is.LessThan(VoxelGibs.Corpses * VoxelGibs.BodiesPerCorpse), "made a few a frame");

            SceneManager.SetActiveScene(test);
            yield return SceneManager.UnloadSceneAsync(level);
        }

        [Test]
        public void APoseThatCannotBeVoxelized_LeavesTheBodyAsItIs()
        {
            // The top flung 9 m off: past the largest grid the voxelizer takes.
            _upper.position += Vector3.right * 9f;
            LogAssert.Expect(LogType.Warning, new Regex("not voxelized"));
            LogAssert.Expect(LogType.Warning, new Regex(@"\[VOXEL GIBS\].*stays as it is"));
            int before = Flesh(true).Length; // the last test's flesh goes only at the end of the frame

            KilledFrom(new Vector3(0f, 0.5f, -5f));

            Assert.IsTrue(_skin.enabled, "no flesh to take its place: the body stays seen");
            Assert.IsTrue(_animator.enabled, "and keeps moving: its death plays as it would");
            Assert.AreEqual(0, VoxelGibs.Pending);
            Assert.AreEqual(before, Flesh(true).Length, "and no flesh");
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
