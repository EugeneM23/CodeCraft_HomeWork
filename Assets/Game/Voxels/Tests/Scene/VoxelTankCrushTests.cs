using System.Collections;
using System.Collections.Generic;
using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Voxels.Tests
{
    /// <summary>
    /// A box "tank" driven into a capsule "enemy" with real physics: fast it crushes the enemy,
    /// sprays blood from under itself and stains its voxel skin; slow it only shoves.
    /// </summary>
    public sealed class VoxelTankCrushTests
    {
        private readonly List<GameObject> _created = new();

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

        private GameObject Track(GameObject go)
        {
            _created.Add(go);
            return go;
        }

        private void Floor()
        {
            GameObject floor = Track(GameObject.CreatePrimitive(PrimitiveType.Plane));
            floor.layer = PhysicsLayers.Ground;
            floor.transform.localScale = Vector3.one * 4f;
        }

        /// <summary>
        /// A 2 × 1 × 3 m box on Character with a heavy body, a contact receiver and, if asked, a voxel
        /// skin: a hull alone, or with the parts Tank_01 has round it (<paramref name="allParts"/>).
        /// </summary>
        private Rigidbody Tank(bool skin, out TankCrushConfig config, bool allParts = false)
        {
            GameObject tank = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            tank.name = "Tank";
            tank.layer = PhysicsLayers.Character;
            tank.transform.position = new Vector3(0f, 0.55f, -3f);
            tank.transform.localScale = Vector3.one;
            tank.GetComponent<BoxCollider>().size = new Vector3(2f, 1f, 3f);
            Object.Destroy(tank.GetComponent<MeshRenderer>());

            if (skin)
            {
                // VoxelizedModel's default part: a hull of mesh renderers, rebuilt as voxels at Start.
                Transform hull = Part(tank.transform, "HullVisual", Vector3.zero, new Vector3(1.8f, 0.8f, 2.8f));
                if (allParts)
                {
                    Transform turret = Part(hull, "TurretVisual", new Vector3(0f, 0.6f, -0.2f), new Vector3(1.2f, 0.4f, 1.4f));
                    // Low and 1.4 m out past the nose, over what the tank runs down — the gun of a tank
                    // pitched nose-down into a crater.
                    Part(turret, "Gun", new Vector3(0f, -0.35f, 2.6f), new Vector3(0.12f, 0.12f, 2f));
                    for (int i = 0; i < 6; i++)
                        Part(hull, $"Wheel_{i + 1}", new Vector3(i % 2 == 0 ? -1f : 1f, -0.35f, i / 2 - 1f),
                            new Vector3(0.2f, 0.4f, 0.4f));
                    foreach (string end in new[] { "TrackL_Front", "TrackR_Front", "TrackL_Rear", "TrackR_Rear" })
                        Part(hull, end, new Vector3(end[5] == 'L' ? -1f : 1f, -0.2f, end.EndsWith("Front") ? 1.5f : -1.5f),
                            new Vector3(0.25f, 0.5f, 0.6f));
                }

                tank.AddComponent<VoxelizedModel>();
            }

            Rigidbody body = tank.AddComponent<Rigidbody>();
            body.mass = 20000f;
            body.useGravity = false;
            body.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
            CollisionContactReceiver receiver = tank.AddComponent<CollisionContactReceiver>();

            config = new TankCrushConfig();
            _crush = new TankCrushBehaviour(receiver, body, config);
            _crush.Init(_tank = new Entity());
            return body;
        }

        /// <summary>A part root of the skin with one plate under it: a cube's renderer, no collider.</summary>
        private static Transform Part(Transform parent, string name, Vector3 position, Vector3 size)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.localPosition = position;
            GameObject plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(plate.GetComponent<Collider>());
            plate.transform.SetParent(root, false);
            plate.transform.localScale = size;
            return root;
        }

        /// <summary>The voxel body VoxelizedModel built for a part root.</summary>
        private static VoxelBody Body(VoxelizedModel skin, string part)
        {
            foreach (VoxelBody body in skin.Bodies)
                if (body.transform.parent.name == part)
                    return body;
            Assert.Fail($"no voxel body for {part}");
            return null;
        }

        private TankCrushBehaviour _crush;
        private IEntity _tank;

        /// <summary>
        /// A capsule on Enemy with 60 health, tagged as a damageable enemy, as heavy and as damped
        /// as a Regax (10 t, damping 10 000). Its capsule goes off when it dies, as DeathBehaviour does.
        /// It stands in the tank's path, <paramref name="x"/> m off to the right of its middle.
        /// </summary>
        private Health Enemy(float x = 0f)
        {
            GameObject enemy = Track(new GameObject("Enemy") { layer = PhysicsLayers.Enemy });
            enemy.transform.position = new Vector3(x, 1f, 0f);
            CapsuleCollider capsule = enemy.AddComponent<CapsuleCollider>();
            capsule.height = 2f;
            capsule.radius = 0.4f;
            Rigidbody body = enemy.AddComponent<Rigidbody>();
            body.mass = 10000f;
            body.linearDamping = 10000f;
            body.freezeRotation = true;

            var entity = enemy.AddComponent<MonoEntity>();
            ((ISerializationCallbackReceiver)entity).OnAfterDeserialize();
            entity.AddEnemyTag();
            entity.AddDamageableTag();
            Health health = HealthUseCase.AddHealthWithEvents(entity, 60);
            health.OnHealthEmpty += () => capsule.enabled = false;
            return health;
        }

        private static int Drops() => VoxelWorld.Instance.Debris.Count + VoxelWorld.Instance.Debris.LyingCount;

        /// <summary>Holds the tank at a speed, ticking the crush before each physics step as the entity would.</summary>
        private IEnumerator Drive(Rigidbody tank, float speed, float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.fixedDeltaTime)
            {
                tank.linearVelocity = new Vector3(0f, 0f, speed);
                _crush.FixedTick(_tank, Time.fixedDeltaTime);
                yield return new WaitForFixedUpdate();
            }
        }

        [UnityTest]
        public IEnumerator RunningDownAHeavyEnemy_TheTankKeepsItsSpeed()
        {
            Floor();
            Health health = Enemy();
            Rigidbody tank = Tank(skin: false, out _);
            Physics.SyncTransforms();

            // Pushed off once and left to coast: no drag, no gravity, nothing but the enemy in the way.
            tank.linearVelocity = new Vector3(0f, 0f, 4f);
            for (float t = 0f; t < 1.5f; t += Time.fixedDeltaTime)
            {
                // The crush ticks before the physics step, as the entity's FixedTick does.
                yield return new WaitForFixedUpdate();
                _crush.FixedTick(_tank, Time.fixedDeltaTime);
            }

            Assert.IsTrue(health.IsEmpty(), "the enemy died under the tank");
            Assert.That(tank.position.z, Is.GreaterThan(1f), "the tank went on past where it stood");
            Assert.That(tank.linearVelocity.z, Is.GreaterThan(3.9f), "a ten-tonne enemy took none of its speed");
        }

        [UnityTest]
        public IEnumerator Fast_TheTankCrushesTheEnemy_AndBloodSpraysFromUnderIt()
        {
            Floor();
            Health health = Enemy();
            Rigidbody tank = Tank(skin: false, out TankCrushConfig config);
            Physics.SyncTransforms();
            int before = Drops();

            yield return Drive(tank, 4f, 1.5f);

            Assert.IsTrue(health.IsEmpty(), "run down at 4 m/s, it died");
            Assert.That(Drops() - before, Is.GreaterThanOrEqualTo(3 * config.dropsPerFan),
                "three fans of blood from under the tracks");
        }

        [UnityTest]
        public IEnumerator Slow_TheTankOnlyShovesTheEnemy()
        {
            Floor();
            Health health = Enemy();
            Rigidbody tank = Tank(skin: false, out TankCrushConfig config);
            Physics.SyncTransforms();
            Assert.That(0.5f, Is.LessThan(config.minSpeed));

            yield return Drive(tank, 0.5f, 6f);

            Assert.AreEqual(60, health.GetCurrent(), "at 0.5 m/s it is pushed, not crushed");
        }

        [UnityTest]
        public IEnumerator Crushing_StainsTheSkinWithBlood()
        {
            Floor();
            Enemy();
            Rigidbody tank = Tank(skin: true, out _);
            Physics.SyncTransforms();
            yield return null; // the skin is built at Start
            yield return null;

            VoxelizedModel skin = tank.GetComponent<VoxelizedModel>();
            Assert.That(skin.Bodies.Count, Is.GreaterThan(0));
            int solid = skin.Bodies[0].SolidCount;

            yield return Drive(tank, 4f, 1.5f);

            Assert.That(skin.Bodies[0].SolidCount, Is.GreaterThan(solid), "blood laid onto the hull as voxels");
        }

        /// <summary>Runs the enemy down with the full-part tank; how many blood voxels each part took.</summary>
        private IEnumerator CrushWithAllParts(float enemyX, Dictionary<string, int> added, int splashes = 0)
        {
            Floor();
            Enemy(enemyX);
            Rigidbody tank = Tank(skin: true, out TankCrushConfig config, allParts: true);
            if (splashes > 0)
                config.splashes = splashes;
            Physics.SyncTransforms();
            yield return null; // the skin is built at Start
            yield return null;

            VoxelizedModel skin = tank.GetComponent<VoxelizedModel>();
            var before = new Dictionary<VoxelBody, int>();
            foreach (VoxelBody body in skin.Bodies)
                before[body] = body.SolidCount;

            yield return Drive(tank, 4f, 1.5f);

            int total = 0;
            foreach (VoxelBody body in skin.Bodies)
            {
                int took = body.SolidCount - before[body];
                added[body.transform.parent.name] = took;
                total += took;
            }
            added["total"] = total;
        }

        [UnityTest]
        public IEnumerator Crushing_HeadOn_StainsTheFront_NotTheBack()
        {
            var added = new Dictionary<string, int>();
            yield return CrushWithAllParts(0f, added);

            int total = added["total"];
            Assert.That(total, Is.GreaterThan(0), "blood laid onto the skin as voxels");
            Assert.That(added["TrackL_Front"] + added["TrackR_Front"], Is.GreaterThan(0), "the track ends at the front take blood");
            Assert.AreEqual(0, added["TrackL_Rear"] + added["TrackR_Rear"], "the back of the tank stays clean");
            Assert.That(added["HullVisual"], Is.GreaterThan(0), "the hull's nose takes blood");
            // Low over the burst, the gun is in it; picked as one part of many it took a part's share
            // and, wrapped round by every splash, went all red.
            Assert.That(added["Gun"], Is.LessThan(total * 0.15f), "a thin gun over the burst takes about its size's share");
        }

        [UnityTest]
        public IEnumerator Crushing_OffToTheRight_StainsTheRightMore()
        {
            var added = new Dictionary<string, int>();
            // More splashes than the game throws, so the count of each side is steady.
            yield return CrushWithAllParts(0.8f, added, splashes: 400);

            int Side(char side) => added[$"Track{side}_Front"] +
                                   (side == 'R' ? added["Wheel_2"] + added["Wheel_4"] + added["Wheel_6"]
                                       : added["Wheel_1"] + added["Wheel_3"] + added["Wheel_5"]);
            Assert.That(added["total"], Is.GreaterThan(0), "blood laid onto the skin as voxels");
            Assert.That(Side('R'), Is.GreaterThan(Side('L') * 1.3f),
                "burst against the right of the nose, the right front takes clearly more than the left");
            Assert.AreEqual(0, added["TrackL_Rear"] + added["TrackR_Rear"], "the back stays clean");
        }
    }
}
