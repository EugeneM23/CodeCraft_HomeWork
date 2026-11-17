using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Voxels.Tests
{
    public sealed class VoxelBodySettleTests
    {
        private readonly List<GameObject> _spawned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _spawned)
                if (go != null)
                    Object.Destroy(go);
            _spawned.Clear();

            foreach (VoxelBody body in Object.FindObjectsByType<VoxelBody>(FindObjectsSortMode.None))
                Object.Destroy(body.gameObject);
        }

        private void Floor()
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.transform.localScale = Vector3.one * 4f;
            _spawned.Add(floor);
            Physics.SyncTransforms();
        }

        private static VoxelBodySettings Loose(bool keepDebris = false, float fractureSpeed = 0f)
        {
            VoxelBodySettings settings = VoxelBodySettings.Default(0.1f);
            settings.Anchor = VoxelAnchor.Largest;
            settings.Layer = VoxelWorld.PieceLayer;
            settings.Settle = true;
            settings.KeepDebris = keepDebris;
            settings.FractureSpeed = fractureSpeed;
            return settings;
        }

        private static VoxelBody Body(int3 size, Vector3 position, Func<int3, bool> solid, VoxelBodySettings settings)
        {
            var voxels = new NativeArray<byte>(size.x * size.y * size.z, Allocator.Persistent);
            for (int z = 0; z < size.z; z++)
            for (int y = 0; y < size.y; y++)
            for (int x = 0; x < size.x; x++)
                if (solid(new int3(x, y, z)))
                    voxels[x + size.x * (y + size.y * z)] = 1;

            var palette = new NativeArray<Color32>(256, Allocator.Persistent);
            palette[1] = new Color32(200, 60, 60, 255);

            var go = new GameObject("Flesh") { layer = VoxelWorld.PieceLayer };
            go.transform.position = position;
            VoxelBody body = go.AddComponent<VoxelBody>();
            body.Init(size, voxels, palette, settings);
            return body;
        }

        private static IEnumerator Settle(VoxelBody body)
        {
            float until = Time.time + 6f;
            while (body.IsLoose && Time.time < until)
                yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator Launch_LetsTheBodyGoWithItsVelocityAndSpin()
        {
            VoxelBody body = Body(new int3(4, 4, 4), new Vector3(0f, 5f, 0f), _ => true, Loose());
            body.Launch(new Vector3(3f, 0f, 0f), new Vector3(0f, 2f, 0f));
            // Kinematic until its colliders are built at the end of the frame.
            yield return null;
            yield return new WaitForFixedUpdate();

            Rigidbody rigidbody = body.GetComponent<Rigidbody>();
            Assert.IsNotNull(rigidbody);
            Assert.IsFalse(rigidbody.isKinematic);
            Assert.That(rigidbody.linearVelocity.x, Is.GreaterThan(2f));
            Assert.That(rigidbody.angularVelocity.y, Is.GreaterThan(1f));
            Assert.IsNotNull(body.GetComponent<VoxelSettle>());
        }

        /// <summary>Waits in physics steps: it comes back in the very step the body settled in.</summary>
        private static IEnumerator SettleInStep(VoxelBody body)
        {
            float until = Time.time + 6f;
            while (body.IsLoose && Time.time < until)
                yield return new WaitForFixedUpdate();
        }

        [UnityTest]
        public IEnumerator Settled_StopsSimulatingButKeepsItsColliders()
        {
            Floor();
            VoxelBody body = Body(new int3(4, 4, 4), new Vector3(0f, 0.3f, 0f), _ => true, Loose());
            body.Launch(Vector3.zero, Vector3.zero);
            yield return Settle(body);

            Assert.IsFalse(body.IsLoose, "it settled within 6 s");
            Rigidbody rigidbody = body.GetComponent<Rigidbody>();
            Assert.IsNotNull(rigidbody, "it keeps its body");
            Assert.IsTrue(rigidbody.isKinematic, "out of the simulation");
            Assert.That(body.GetComponentsInChildren<BoxCollider>().Any(b => b.enabled));
        }

        [UnityTest]
        public IEnumerator Settled_TwoBlastsInTheStepItSettledIn_WakeItOnce()
        {
            Floor();
            VoxelBody body = Body(new int3(6, 4, 6), new Vector3(0f, 0.3f, 0f), _ => true, Loose());
            body.Launch(Vector3.zero, Vector3.zero);
            yield return SettleInStep(body);
            Assert.IsFalse(body.IsLoose, "it settled within 6 s");
            Rigidbody rigidbody = body.GetComponent<Rigidbody>();

            // Same step as the freeze: a shell's blast (OnCollisionEnter) can come right after it.
            Vector3 centre = body.WorldBounds.center;
            Assert.DoesNotThrow(() =>
            {
                Assert.That(VoxelDamage.Blast(centre + Vector3.right * 0.12f, 0.15f, 9f), Is.GreaterThan(0));
                Assert.That(VoxelDamage.Blast(centre - Vector3.right * 0.12f, 0.15f, 9f), Is.GreaterThan(0));
            });

            Assert.IsTrue(body.IsLoose, "a blast wakes it");
            Assert.AreEqual(1, body.GetComponents<Rigidbody>().Length, "once, with the body it had");
            Assert.AreSame(rigidbody, body.GetComponent<Rigidbody>());

            yield return null;
            yield return new WaitForFixedUpdate();
            Assert.IsFalse(rigidbody.isKinematic, "let go after the frame's rebuild");
        }

        [UnityTest]
        public IEnumerator Settled_SomethingLandingHardOnIt_DoesNotBreakIt()
        {
            Floor();
            VoxelBody body = Body(new int3(6, 4, 6), new Vector3(0f, 0.3f, 0f), _ => true, Loose(fractureSpeed: 1f));
            body.Launch(Vector3.zero, Vector3.zero);
            yield return Settle(body);
            Assert.IsFalse(body.IsLoose, "it settled within 6 s");
            int solid = body.SolidCount;

            // A block dropped from 3 m lands at ~7.7 m/s, far over the piece's 1 m/s.
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _spawned.Add(block);
            block.transform.localScale = Vector3.one * 0.2f;
            block.transform.position = body.WorldBounds.center + Vector3.up * 3f;
            block.AddComponent<Rigidbody>().mass = 50f;
            Physics.SyncTransforms();
            yield return new WaitForSeconds(1.2f);

            float top = body.WorldBounds.max.y;
            Assert.That(block.transform.position.y, Is.InRange(top, top + 0.2f), "it came down on the piece and lies there");
            Assert.AreEqual(solid, body.SolidCount, "nothing broke off");
            Assert.IsFalse(body.IsLoose);
        }

        [UnityTest]
        public IEnumerator Settled_ABlastThrowsItAgain_ABulletOnlyChips()
        {
            Floor();
            VoxelBody body = Body(new int3(4, 4, 4), new Vector3(0f, 0.3f, 0f), _ => true, Loose());
            body.Launch(Vector3.zero, Vector3.zero);
            yield return Settle(body);
            Assert.IsFalse(body.IsLoose);

            Vector3 top = body.WorldBounds.center + Vector3.up * 0.15f;
            VoxelDamage.Impact(top, Vector3.down, 0.13f);
            yield return null;
            Assert.IsFalse(body.IsLoose, "a bullet's chip does not wake it");

            // Not at the chip: the bullet already emptied the voxels there, a blast must reach solid ones.
            int taken = VoxelDamage.Blast(body.WorldBounds.center, 0.25f, 9f);
            Assert.That(taken, Is.GreaterThan(0));
            yield return null;
            Assert.IsTrue(body.IsLoose, "a blast does");
        }

        [UnityTest]
        public IEnumerator SplitLoose_AnIslandFliesOnWithTheBody()
        {
            // A 6×6×6 block and, past a gap, a 3×3×3 one: 27 voxels, more than a clod.
            VoxelBody body = Body(new int3(11, 6, 6), new Vector3(0f, 5f, 0f),
                c => c.x < 6 || (c.x >= 8 && c.y < 3 && c.z < 3), Loose());
            body.Launch(new Vector3(0f, 0f, 4f), Vector3.zero);
            body.SplitLoose();
            yield return null;
            yield return new WaitForFixedUpdate();

            VoxelBody island = Object.FindObjectsByType<VoxelBody>(FindObjectsSortMode.None).Single(b => b != body);
            Assert.AreEqual(27, island.SolidCount);
            Assert.That(island.GetComponent<Rigidbody>().linearVelocity.z, Is.GreaterThan(3f));
        }

        [UnityTest]
        public IEnumerator KeepDebris_WhatACarveThrowsLiesForGood()
        {
            Floor();
            VoxelBody body = Body(new int3(8, 4, 8), new Vector3(-0.4f, 0f, -0.4f), _ => true, Loose(keepDebris: true));
            yield return null;

            VoxelDebris debris = VoxelWorld.Instance.Debris;
            int before = debris.LyingCount;
            VoxelDamage.Blast(body.WorldBounds.center + Vector3.up * 0.2f, 0.25f, 3f);

            float until = Time.time + 8f;
            while (debris.LyingCount == before && Time.time < until)
                yield return null;
            Assert.That(debris.LyingCount, Is.GreaterThan(before));
        }

        [Test]
        public void FractureSpeed_SetsTheBreakThreshold()
        {
            VoxelBody body = Body(new int3(2, 2, 2), Vector3.up * 5f, _ => true, Loose(fractureSpeed: 7f));
            body.Launch(Vector3.zero, Vector3.zero);
            Assert.AreEqual(7f, body.GetComponent<VoxelImpactFracture>().MinSpeed);
        }
    }
}
