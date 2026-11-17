using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Voxels.Tests
{
    public sealed class VoxelPaintTests
    {
        private const string TANK = "Assets/Game/Prefabs/Vehicles/Tank_01.prefab";

        /// <summary>
        /// Hash of every part's size, voxels and palette from VoxelizedModel.Voxelize() on Tank_01, taken on the
        /// code before the paint moved to VoxelPaint (commit d7e304c3b; the same with Burst on and off).
        /// </summary>
        private const ulong TANK_HASH_BEFORE_MOVE = 0x99FAC896B21BBE63UL;

        [Test]
        public void VoxelizedModel_OnTank01_GivesTheSameGridsAndPalettesAsBeforeTheMove()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(TANK), scene);
                List<VoxelizedModel.Part> parts = instance.GetComponent<VoxelizedModel>().Voxelize();
                ulong hash = Hash(parts);
                foreach (VoxelizedModel.Part part in parts)
                {
                    part.Grid.Voxels.Dispose();
                    part.Grid.Palette.Dispose();
                }

                Assert.AreEqual(23, parts.Count);
                Assert.AreEqual(TANK_HASH_BEFORE_MOVE.ToString("X16"), hash.ToString("X16"));
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        [Test]
        public void Camouflage_OneSlotPerColour_RepaintsThePaintSlotOnly_AndAddsEachColourOnce()
        {
            var size = new int3(20, 20, 20);
            var voxels = new NativeArray<byte>(size.x * size.y * size.z, Allocator.TempJob);
            var palette = new NativeArray<Color32>(256, Allocator.TempJob);
            palette[1] = MeshVoxelizer.ToVertexColor(Color.green);
            palette[2] = MeshVoxelizer.ToVertexColor(Color.blue);
            for (int i = 0; i < voxels.Length; i++)
                voxels[i] = (byte)(i % 2 == 0 ? 1 : 2);
            var camo = new VoxelizedModel.Camouflage
            {
                Patch = Color.red, Dark = Color.black, Light = Color.yellow, Frequency = 1.5f, Threshold = 0.2f
            };

            int painted = VoxelPaint.Camouflage(voxels, size, float3.zero, 0.1f, float4x4.identity, palette, 1, 1, camo, 0f);
            int again = VoxelPaint.Camouflage(voxels, size, float3.zero, 0.1f, float4x4.identity, palette, 1, 1, camo, 0f);

            int blue = 0, outside = 0;
            for (int i = 0; i < voxels.Length; i++)
            {
                if (i % 2 == 1 && voxels[i] == 2) blue++;
                if (voxels[i] > 5) outside++;
            }

            Assert.That(painted, Is.GreaterThan(0));
            Assert.AreEqual(voxels.Length / 2, blue, "other colours stay");
            Assert.AreEqual(0, outside, "patch, dark and light take slots 3..5, a second call reuses them");
            Assert.That(again, Is.LessThan(painted), "the second call sees only what is still the paint colour");
            Assert.AreEqual(0, palette[6].a);
            voxels.Dispose();
            palette.Dispose();
        }

        private static ulong Hash(List<VoxelizedModel.Part> parts)
        {
            ulong h = 14695981039346656037UL; // FNV-1a
            void Add(int b) { h ^= (byte)b; h *= 1099511628211UL; }
            void Int(int v) { Add(v); Add(v >> 8); Add(v >> 16); Add(v >> 24); }
            foreach (VoxelizedModel.Part part in parts)
            {
                Int(part.Grid.Size.x); Int(part.Grid.Size.y); Int(part.Grid.Size.z);
                foreach (byte v in part.Grid.Voxels) Add(v);
                foreach (Color32 c in part.Grid.Palette) { Add(c.r); Add(c.g); Add(c.b); Add(c.a); }
            }

            return h;
        }
    }
}
