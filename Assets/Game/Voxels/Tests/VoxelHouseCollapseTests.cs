using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Voxels.Tests
{
    /// <summary>
    /// A house left standing on one corner post: the shot that takes the post must drop the house,
    /// not fling it spinning across the map (the "house flies into space" bug).
    /// </summary>
    public sealed class VoxelHouseCollapseTests
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

        [UnityTest]
        public IEnumerator House_LastPostShot_FallsInsteadOfFlyingOff([Values(9f, 24f)] float force)
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(60f, 1f, 60f);
            _spawned.Add(ground);

            VoxelBody standing = SpawnHouse(new Vector3Int(64, 78, 54));
            yield return null;

            // Scoop out the bottom 60 cm all round, but for a post at one corner.
            Bounds bounds = standing.WorldBounds;
            Vector3 post = new Vector3(bounds.min.x + 0.25f, 0f, bounds.min.z + 0.25f);
            for (float x = bounds.min.x; x <= bounds.max.x + 0.2f; x += 0.3f)
            for (float z = bounds.min.z; z <= bounds.max.z + 0.2f; z += 0.3f)
            for (float y = 0.05f; y <= 0.55f; y += 0.25f)
            {
                var at = new Vector3(x, y, z);
                if (Mathf.Abs(x - post.x) < 0.6f && Mathf.Abs(z - post.z) < 0.6f)
                    continue;
                standing.Dig(at, 0.3f, Vector3.zero, 0);
            }

            yield return null;
            yield return new WaitForFixedUpdate();
            Assert.IsNull(standing.GetComponent<Rigidbody>(), "the post still holds the house up");

            // A shell (9 m/s) or a bomb's push (24 m/s) takes the post: the whole house is loose now.
            VoxelDamage.Blast(post + Vector3.up * 0.3f, 0.6f, force);
            yield return null;

            // The house is by far the heaviest piece; it stays that body even when it breaks on landing.
            Rigidbody house = Object.FindObjectsByType<Rigidbody>(FindObjectsSortMode.None)
                .OrderByDescending(r => r.mass).FirstOrDefault();
            Assert.IsNotNull(house, "the house came off its post");
            Assert.That(house.mass, Is.GreaterThan(5000f));
            Vector3 start = house.worldCenterOfMass;

            float maxSpeed = 0f, maxSpin = 0f, maxRise = 0f, maxDrift = 0f;
            float end = Time.time + 4f;
            while (Time.time < end && house != null)
            {
                yield return new WaitForFixedUpdate();
                if (house == null)
                    break;
                maxSpeed = Mathf.Max(maxSpeed, house.linearVelocity.magnitude);
                maxSpin = Mathf.Max(maxSpin, house.angularVelocity.magnitude);
                Vector3 delta = house.worldCenterOfMass - start;
                maxRise = Mathf.Max(maxRise, delta.y);
                maxDrift = Mathf.Max(maxDrift, new Vector2(delta.x, delta.z).magnitude);
            }

            Debug.Log($"[HouseCollapse] force {force} maxSpeed {maxSpeed:F1} maxSpin {maxSpin:F1} " +
                      $"maxRise {maxRise:F2} maxDrift {maxDrift:F1}");

            // Dropped from 60 cm it lands at ~3.5 m/s and may roll over (with no push at all it
            // spins up to ~3.6 rad/s doing so); before the fix it took off at 50 m/s.
            Assert.That(maxRise, Is.LessThan(0.5f), "the house rose");
            Assert.That(maxSpeed, Is.LessThan(6f), "the house was flung");
            Assert.That(maxSpin, Is.LessThan(5f), "the house spun");
            Assert.That(maxDrift, Is.LessThan(4f), "the house travelled");
        }

        private VoxelBody SpawnHouse(Vector3Int size)
        {
            var go = new GameObject("House");
            go.SetActive(false);
            VoxelStructure structure = go.AddComponent<VoxelStructure>();
            Set(structure, "_shape", VoxelStructure.Shape.Victorian);
            Set(structure, "_size", size);
            go.SetActive(true);
            _spawned.Add(go);
            structure.Build();
            return go.GetComponentInChildren<VoxelBody>();
        }

        private static void Set<T>(Object target, string field, T value)
        {
            var info = target.GetType().GetField(field,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            info.SetValue(target, value);
        }
    }
}
