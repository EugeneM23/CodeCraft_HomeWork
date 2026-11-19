using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Voxels.Tests
{
    /// <summary>
    /// Every blast in the level (ExplosionEvents) reaches the houses' windows through HouseGlassBreaker: a
    /// high-explosive shell's (5 m) blows them out, an armour-piercing shell's (1.25 m) does not.
    /// </summary>
    public sealed class HouseGlassBreakerTests
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
        public IEnumerator HighExplosiveBlast_BlowsTheWindowsOut_ArmourPiercingDoesNot()
        {
            VoxelBody house = SpawnHouse();
            yield return null;
            int whole = house.GlassCount;
            // In front of the middle first-floor window (VoxelHouseGlassTests), 1.5 m out.
            Vector3 front = house.transform.TransformPoint((Vector3)(house.Origin + (new float3(33, 44, 7) + 0.5f) * house.VoxelSize)) +
                            Vector3.back * 1.5f;

            ExplosionEvents.Raise(front, 1.25f);
            yield return null;
            Assert.AreEqual(whole, house.GlassCount, "an AP shell's small blast leaves the glass");

            ExplosionEvents.Raise(front, 5f);
            yield return null;
            Debug.Log($"[Glass] HE blast: {whole - house.GlassCount} of {whole} glass voxels broke");
            Assert.That(house.GlassCount, Is.LessThan(whole - 300), "an HE shell's blast blows the front's windows out");
        }

        private VoxelBody SpawnHouse()
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
