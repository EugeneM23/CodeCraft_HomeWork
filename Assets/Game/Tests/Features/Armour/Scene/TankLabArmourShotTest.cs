using System.Collections;
using System.Collections.Generic;
using Game.Voxels;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests
{
    /// <summary>
    /// The lab's target tank (TankDummy_01, armoured by its TankArmourConfig) is shot with an armour-piercing shell
    /// at known angles: a graze off the hull's side ricochets, the same side head-on is pierced, the gun mantlet
    /// head-on holds. No frames are waited for render (batchmode safe): only physics and the voxel build.
    /// </summary>
    public class TankLabArmourShotTest
    {
        private static readonly ArmourShell Shell = new(125f, 88f);

        private GameObject _tank;
        private readonly List<ArmourHit> _hits = new();
        private readonly List<Vector3> _path = new();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            var prefab = Resources.Load<GameObject>("TankDummy_01");
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponent<TankArmour>(), Is.Not.Null, "TankDummy_01 has no TankArmour (Tools/Combat/Set Up Tank Armour)");
            _tank = Object.Instantiate(prefab, new Vector3(300f, 50f, 300f), Quaternion.identity);
            for (int i = 0; i < 600 && Hull() == null; i++)
                yield return null;
            Assert.That(Hull(), Is.Not.Null, "the target's hull never got its voxel body");
            // The bodies' box colliders come with their first meshes.
            for (int i = 0; i < 600 && Hull().GetComponentsInChildren<Collider>().Length == 0; i++)
                yield return null;
            for (int i = 0; i < 10; i++)
                yield return new WaitForFixedUpdate();
            Physics.SyncTransforms();
        }

        [TearDown]
        public void TearDown()
        {
            if (_tank != null)
                Object.Destroy(_tank);
        }

        [UnityTest]
        public IEnumerator AGrazeOffTheHullSide_Ricochets()
        {
            Assert.That(SideHit(out RaycastHit side, out Vector3 normal), Is.True, "no hull side found");
            Vector3 along = Vector3.ProjectOnPlane(Vector3.back, normal).normalized;
            Vector3 dir = (-normal * Mathf.Cos(80f * Mathf.Deg2Rad) + along * Mathf.Sin(80f * Mathf.Deg2Rad)).normalized;
            ArmourHit hit = Shoot(side.point - dir * 0.6f, dir, 0f);
            Assert.That(hit.Result.Outcome, Is.EqualTo(ArmourOutcome.Ricochet), TankArmourLab.Line(hit));
            Assert.That(hit.Result.Angle, Is.GreaterThan(70f));
            // It flew on: the path goes off past the hit.
            Assert.That(_path.Count, Is.GreaterThan(2));
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheHullSideHeadOn_IsPierced()
        {
            Assert.That(SideHit(out RaycastHit side, out Vector3 normal), Is.True, "no hull side found");
            ArmourHit hit = Shoot(side.point + normal * 3f, -normal, -1f);
            Assert.That(hit.Zone, Does.Contain("орт"), TankArmourLab.Line(hit));
            Assert.That(hit.Result.Outcome, Is.EqualTo(ArmourOutcome.Penetration), TankArmourLab.Line(hit));
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheGunMantletHeadOn_Holds()
        {
            VoxelBody gun = Body("Gun");
            Assert.That(gun, Is.Not.Null);
            Bounds b = gun.WorldBounds;
            Vector3 aim = new Vector3(b.center.x, b.center.y + 0.1f, b.center.z);
            ArmourHit hit = Shoot(aim + Vector3.forward * 8f, Vector3.back, -1f);
            Assert.That(hit.Result.Outcome, Is.EqualTo(ArmourOutcome.NoPenetration), TankArmourLab.Line(hit));
            Assert.That(hit.Result.Effective, Is.GreaterThanOrEqualTo(hit.Result.Penetration));
            yield return null;
        }

        /// <summary>The first armour event of a shell (the ground does not count).</summary>
        private ArmourHit Shoot(Vector3 from, Vector3 dir, float roll)
        {
            ArmourShotUseCase.Trace(from, dir, Shell, roll, _hits, _path);
            foreach (ArmourHit hit in _hits)
            {
                Debug.Log("[ArmourTest] " + (hit.IsTerrain ? "ground" : TankArmourLab.Line(hit)));
                if (!hit.IsTerrain)
                    return hit;
            }

            Assert.Fail("the shell struck no armour");
            return default;
        }

        /// <summary>The hull's right side, high up and half way along: struck square from the right.</summary>
        private bool SideHit(out RaycastHit hit, out Vector3 normal)
        {
            VoxelBody hull = Hull();
            Bounds b = hull.WorldBounds;
            var from = new Vector3(b.max.x + 2f, Mathf.Lerp(b.min.y, b.max.y, 0.7f), b.center.z);
            normal = Vector3.right;
            if (!Physics.Raycast(from, Vector3.left, out hit, 6f, ~0, QueryTriggerInteraction.Ignore))
                return false;
            if (hit.collider.GetComponentInParent<VoxelBody>() != hull)
                return false;
            normal = ArmourSurfaceUseCase.SmoothNormal(hit, Vector3.left);
            return true;
        }

        private VoxelBody Hull() => Body("HullVisual");

        private VoxelBody Body(string part)
        {
            if (_tank == null)
                return null;
            foreach (VoxelBody body in _tank.GetComponentsInChildren<VoxelBody>())
                if (body.transform.parent != null && body.transform.parent.name == part && body.name == "Voxels")
                    return body;
            return null;
        }
    }
}
