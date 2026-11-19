using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Voxels.Tests
{
    /// <summary>
    /// A tank-sized ram (3 t, a 2 × 0.8 × 2.8 m hull held at its height, as the suspension holds it) driven at a
    /// Victorian house's front, its drive pulling up to 16 m/s² as TankDriveBehaviour's does (VoxelRammer):
    /// slow, it stops against the wall; at speed it breaks through and slows; at boosted top speed it takes the
    /// skeleton too; stopped by the house, it does not bulldoze on at a crawl.
    /// </summary>
    public sealed class VoxelRamTests
    {
        private readonly List<GameObject> _spawned = new();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            for (int pass = 0; pass < 2; pass++)
            {
                foreach (GameObject go in _spawned)
                    if (go != null)
                        Object.Destroy(go);
                foreach (VoxelBody body in Object.FindObjectsByType<VoxelBody>(FindObjectsSortMode.None))
                    Object.Destroy(body.gameObject);
                yield return null;
            }

            _spawned.Clear();
        }

        [UnityTest]
        public IEnumerator Slow_StopsAgainstTheWall()
        {
            (VoxelBody house, float wall) = SpawnHouse();
            int whole = house.SolidCount;
            Driver ram = SpawnRam(wall, 2.5f);
            yield return Drive(3f);

            float front = ram.Front;
            Debug.Log($"[Ram] slow: front {front - wall:F2} m past the wall, house lost {whole - house.SolidCount}");
            Assert.That(front, Is.LessThan(wall + 0.3f), "it stopped at the wall");
            Assert.That(whole - house.SolidCount, Is.LessThan(100), "it broke next to nothing");
        }

        [UnityTest]
        public IEnumerator Fast_BreaksThrough_AndSlows()
        {
            (VoxelBody house, float wall) = SpawnHouse();
            int whole = house.SolidCount;
            Driver ram = SpawnRam(wall, 6f);
            float slowest = float.MaxValue;
            float end = Time.time + 3f;
            while (Time.time < end)
            {
                yield return new WaitForFixedUpdate();
                if (ram.Front > wall)
                    slowest = Mathf.Min(slowest, ram.Speed);
            }

            Debug.Log($"[Ram] top speed: front {ram.Front - wall:F2} m past the wall, slowest {slowest:F1} m/s, house lost {whole - house.SolidCount}");
            Assert.That(ram.Front, Is.GreaterThan(wall + 1f), "it broke into the house");
            Assert.That(whole - house.SolidCount, Is.GreaterThan(400), "a hole the size of the bow");
            Assert.That(slowest, Is.LessThan(5.5f), "breaking cost it speed");
        }

        [UnityTest]
        public IEnumerator Boosted_TakesTheSkeletonToo()
        {
            (VoxelBody house, float wall) = SpawnHouse();
            int hard = Hard(house);
            Driver ram = SpawnRam(wall, 10.8f);
            yield return Drive(2f);

            int lost = hard - Hard(house);
            Debug.Log($"[Ram] boosted: front {ram.Front - wall:F2} m past the wall, skeleton voxels lost {lost} of {hard}");
            // 1.8–3.4 m over runs: the furniture, the stairs and the rubble falling from over the hole stop it at
            // different depths; past the front wall and well into the room either way.
            Assert.That(ram.Front, Is.GreaterThan(wall + 1.5f), "it went deep into the house");
            Assert.That(lost, Is.GreaterThan(150), "it took piers or beams of the skeleton");
        }

        [UnityTest]
        public IEnumerator Stopped_DoesNotBulldozeOnAtACrawl()
        {
            (VoxelBody house, float wall) = SpawnHouse();
            // Just over the break speed: it breaks the front, is spent, and must not chew on through the house.
            Driver ram = SpawnRam(wall, 3.7f);
            yield return Drive(1.5f);
            int afterRam = house.SolidCount;
            float front = ram.Front;
            ram.Target = 8f;
            yield return Drive(2f);

            Debug.Log($"[Ram] crawl: front {front - wall:F2} -> {ram.Front - wall:F2} m, lost after the ram {afterRam - house.SolidCount}");
            Assert.That(afterRam - house.SolidCount, Is.LessThan(150), "the drive's pull does not break on through");
        }

        private static IEnumerator Drive(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end)
                yield return new WaitForFixedUpdate();
        }

        private static int Hard(VoxelBody body)
        {
            int hard = 0;
            for (int z = 0; z < body.Size.z; z++)
            for (int y = 0; y < body.Size.y; y++)
            for (int x = 0; x < body.Size.x; x++)
                if (body.IsHard(body.VoxelAt(new int3(x, y, z))))
                    hard++;
            return hard;
        }

        /// <summary>A two-storey Victorian on flat ground at the origin; returns it and its front wall's z.</summary>
        private (VoxelBody house, float wall) SpawnHouse()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(60f, 1f, 60f);
            _spawned.Add(ground);

            var go = new GameObject("House");
            go.SetActive(false);
            VoxelStructure structure = go.AddComponent<VoxelStructure>();
            Set(structure, "_shape", VoxelStructure.Shape.Victorian);
            Set(structure, "_size", new Vector3Int(64, 106, 54));
            go.SetActive(true);
            _spawned.Add(go);
            structure.Build();
            VoxelBody house = go.GetComponentInChildren<VoxelBody>();
            // The front wall's outer face: 6 voxels in from the grid (VoxelStructure.Victorian).
            return (house, house.WorldBounds.min.z + 0.6f);
        }

        /// <summary>The ram 4 m in front of the wall, facing it, driving at <paramref name="speed"/>.</summary>
        private Driver SpawnRam(float wall, float speed)
        {
            var go = new GameObject("Ram");
            go.transform.position = new Vector3(0f, 0f, wall - 4f - 1.4f);
            var body = go.AddComponent<Rigidbody>();
            body.mass = 3000f;
            body.useGravity = false;
            body.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
            body.collisionDetectionMode = CollisionDetectionMode.Continuous;
            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.85f, 0f);
            box.size = new Vector3(2f, 0.8f, 2.8f);
            box.material = new PhysicsMaterial("Slick") { dynamicFriction = 0f, staticFriction = 0f, frictionCombine = PhysicsMaterialCombine.Minimum };
            go.AddComponent<VoxelRammer>();
            var driver = go.AddComponent<Driver>();
            driver.Target = speed;
            body.linearVelocity = Vector3.forward * speed;
            _spawned.Add(go);
            return driver;
        }

        private static void Set<T>(Object target, string field, T value)
        {
            var info = target.GetType().GetField(field,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            info.SetValue(target, value);
        }

        /// <summary>Pulls the ram along +z toward its target speed, at most 16 m/s² (TankDriveBehaviour.TRACTION).</summary>
        private sealed class Driver : MonoBehaviour
        {
            public float Target;
            private Rigidbody _body;

            public float Speed => _body.linearVelocity.z;
            public float Front => transform.position.z + 1.4f;

            private void Awake() => _body = GetComponent<Rigidbody>();

            private void FixedUpdate()
            {
                float change = Mathf.Clamp(Target - _body.linearVelocity.z, -16f * Time.fixedDeltaTime, 16f * Time.fixedDeltaTime);
                _body.AddForce(Vector3.forward * (change / Time.fixedDeltaTime * _body.mass), ForceMode.Force);
            }
        }
    }
}
