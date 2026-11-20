using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Voxels.Tests
{
    /// <summary>
    /// A body popped under a tank's hull (VoxelSpray.UnderHull): its blood and bits burst out from under the belly to every
    /// side as hard as an HE shell's hit, flat: nothing goes through the hull's box or over it, and it lands metres round.
    /// Green goo, so it lies as cubes instead of soaking in.
    /// </summary>
    public sealed class VoxelUnderHullTests
    {
        private static readonly Color32[] Green = { new(40, 190, 10, 255), new(60, 170, 20, 255) };

        private readonly List<GameObject> _spawned = new();
        private readonly List<Vector3> _flying = new();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _spawned)
                if (go != null)
                    Object.Destroy(go);
            _spawned.Clear();
        }

        [UnityTest]
        public IEnumerator APopUnderTheHull_BurstsOutFlatToEverySide_NothingThroughOrOverTheHull()
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.transform.localScale = new Vector3(100f, 1f, 100f);
            _spawned.Add(floor);
            Physics.SyncTransforms();
            VoxelDebris debris = VoxelWorld.Instance.Debris;

            // A hull 3 m wide, 6 m long, its belly 0.45 m over the ground, turned 30°; the body under it, off its middle.
            var hull = new Vector3(150f, 0.75f, -150f);
            Quaternion turn = Quaternion.Euler(0f, 30f, 0f);
            var half = new Vector3(1.5f, 0.3f, 3f);
            Vector3 body = hull + turn * new Vector3(0.4f, -0.75f, 1.2f);
            HashSet<int> before = LyingNear(debris, hull, 25f).Select(p => p.GetHashCode()).ToHashSet();

            int thrown = VoxelSpray.UnderHull(body, hull, turn, half, 0f, VoxelSpray.UNDER_HULL_DROPS, VoxelSpray.UNDER_HULL_BITS,
                VoxelSpray.UNDER_HULL_SPEED, Green, Green, VoxelSpray.UNDER_HULL_DROP_SIZE, VoxelSpray.UNDER_HULL_BIT_SIZE);
            Assert.Greater(thrown, VoxelSpray.UNDER_HULL_DROPS);

            float top = 0f;
            int inside = 0;
            float until = Time.time + 6f;
            while (Time.time < until)
            {
                debris.GetClusterPositions(_flying);
                bool left = false;
                foreach (Vector3 p in _flying)
                {
                    Vector3 local = Quaternion.Inverse(turn) * (p - hull);
                    if (new Vector2(local.x, local.z).magnitude > 30f)
                        continue;
                    left = true;
                    top = Mathf.Max(top, p.y);
                    // Through the hull: in its footprint and up in its box (over the belly).
                    if (Mathf.Abs(local.x) < half.x && Mathf.Abs(local.z) < half.z && local.y > -half.y)
                        inside++;
                }

                if (!left)
                    break;
                yield return null;
            }

            List<Vector3> landed = LyingNear(debris, hull, 25f).Where(p => !before.Contains(p.GetHashCode())).ToList();
            List<float> reach = landed.Select(p => new Vector2(p.x - body.x, p.z - body.z).magnitude).OrderBy(d => d).ToList();
            float far = reach.Count > 0 ? reach[(int)(reach.Count * 0.9f)] : 0f;
            Debug.Log($"[UnderHull] {landed.Count} cubes landed, median {reach[reach.Count / 2]:F1} m, p90 {far:F1} m, top {top:F2} m, inside the hull {inside}");

            Assert.AreEqual(0, inside, "nothing goes up through the hull");
            Assert.Less(top, hull.y + half.y, "it stays low: nothing goes over the hull's top");
            Assert.Greater(landed.Count, VoxelSpray.UNDER_HULL_DROPS / 2, "it lands round the tank");
            Assert.Greater(far, 4f, "as hard as an HE shell's hit: metres out");

            int quarters = 0;
            for (int q = 0; q < 4; q++)
            {
                float from = q * 90f - 180f;
                if (landed.Any(p =>
                    {
                        float a = Mathf.Atan2(p.x - body.x, p.z - body.z) * Mathf.Rad2Deg;
                        return a >= from && a < from + 90f;
                    }))
                    quarters++;
            }

            Assert.AreEqual(4, quarters, "out to every side");
        }

        private static List<Vector3> LyingNear(VoxelDebris debris, Vector3 at, float radius)
        {
            var near = new List<Vector3>();
            for (int slot = 0; slot < debris.LyingCount; slot++)
            {
                if (!debris.TryGetLying(slot, out Matrix4x4 m, out _))
                    continue;
                Vector3 p = m.GetPosition();
                if (new Vector2(p.x - at.x, p.z - at.z).magnitude < radius)
                    near.Add(p);
            }

            return near;
        }
    }
}
