using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Atomic.Entities;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Voxels.Tests
{
    /// <summary>
    /// Flesh that came to rest lies on Rubble with a kinematic body (VoxelBody.Freeze): characters
    /// and the tank still pass through it (PassThroughLoosePiecesBehaviour), as they do while it flies.
    /// </summary>
    public sealed class VoxelFleshPassThroughTests
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

        /// <summary>A 0.4 m block of flesh, set up as VoxelGibs sets up its pieces.</summary>
        private static VoxelBody Flesh(Vector3 position)
        {
            var size = new int3(8, 8, 8);
            var voxels = new NativeArray<byte>(size.x * size.y * size.z, Allocator.Persistent);
            for (int i = 0; i < voxels.Length; i++)
                voxels[i] = 1;
            var palette = new NativeArray<Color32>(256, Allocator.Persistent);
            palette[1] = new Color32(150, 30, 30, 255);

            VoxelBodySettings settings = VoxelBodySettings.Default(0.05f);
            settings.Anchor = VoxelAnchor.Largest;
            settings.Colliders = true;
            settings.Layer = VoxelWorld.PieceLayer;
            settings.ColliderCell = 2;
            settings.Settle = true;
            settings.KeepDebris = true;

            var go = new GameObject(VoxelGibs.NAME) { layer = settings.Layer };
            go.transform.position = position;
            VoxelBody body = go.AddComponent<VoxelBody>();
            body.Init(size, voxels, palette, settings);
            return body;
        }

        [UnityTest]
        public IEnumerator SettledFlesh_ACharacterPassesThroughIt()
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.transform.localScale = Vector3.one * 4f;
            _created.Add(floor);
            Physics.SyncTransforms();

            VoxelBody flesh = Flesh(new Vector3(0f, 0.2f, 0f));
            flesh.Launch(Vector3.zero, Vector3.zero);
            float until = Time.time + 6f;
            while (flesh.IsLoose && Time.time < until)
                yield return null;
            yield return null;

            Assert.IsFalse(flesh.IsLoose, "it settled within 6 s");
            Assert.AreEqual(PhysicsLayers.Rubble, flesh.gameObject.layer, "flesh lies on Rubble");

            var character = new GameObject("Character") { layer = PhysicsLayers.Character };
            _created.Add(character);
            character.transform.position = flesh.WorldBounds.center + Vector3.right * 0.6f;
            CapsuleCollider capsule = character.AddComponent<CapsuleCollider>();
            capsule.height = 2f;
            capsule.radius = 0.4f;
            Physics.SyncTransforms();

            IEntity entity = new Entity();
            entity.AddGameObject(character);
            var behaviour = new PassThroughLoosePiecesBehaviour();
            behaviour.Init(entity);
            behaviour.FixedTick(entity, Time.fixedDeltaTime);

            BoxCollider[] boxes = flesh.GetComponentsInChildren<BoxCollider>().Where(b => b.enabled).ToArray();
            Assert.That(boxes, Is.Not.Empty);
            foreach (BoxCollider box in boxes)
                Assert.IsTrue(Physics.GetIgnoreCollision(capsule, box), $"{box.name} at {box.center} still blocks");
        }

        [Test]
        public void APileLargerThanTheQueryBuffer_StillPassesWhole()
        {
            // Over 1024 boxes round the owner, as after a few deaths on one spot: the query buffer
            // started at 1024, and the pieces past it used to keep blocking.
            const int PIECES = 1500;
            var pile = new GameObject("Pile");
            _created.Add(pile);
            pile.AddComponent<Rigidbody>().isKinematic = true;
            var boxes = new List<BoxCollider>(PIECES);
            for (int i = 0; i < PIECES; i++)
            {
                var piece = new GameObject("Box") { layer = PhysicsLayers.Rubble };
                piece.transform.SetParent(pile.transform, false);
                piece.transform.localPosition = new Vector3(i % 12, i / 144, (i / 12) % 12) * 0.1f;
                BoxCollider box = piece.AddComponent<BoxCollider>();
                box.size = Vector3.one * 0.1f;
                boxes.Add(box);
            }

            var character = new GameObject("Character") { layer = PhysicsLayers.Character };
            _created.Add(character);
            character.transform.position = new Vector3(0.6f, 0.5f, 0.6f);
            CapsuleCollider capsule = character.AddComponent<CapsuleCollider>();
            capsule.height = 2f;
            capsule.radius = 0.4f;
            Physics.SyncTransforms();

            IEntity entity = new Entity();
            entity.AddGameObject(character);
            var behaviour = new PassThroughLoosePiecesBehaviour();
            behaviour.Init(entity);
            behaviour.FixedTick(entity, Time.fixedDeltaTime);

            int blocking = boxes.Count(box => !Physics.GetIgnoreCollision(capsule, box));
            Assert.AreEqual(0, blocking, $"{blocking} of {PIECES} boxes still block");
        }
    }
}
