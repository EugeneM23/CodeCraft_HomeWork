using System.Collections.Generic;
using System.Reflection;
using Game.Voxels.EditorTools;
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

        [Test]
        public void Voxelizer_WithTank01sPaint_GivesHullAndTurretCamouflage_AndTurretSidesTheDecals()
        {
            var tank = AssetDatabase.LoadAssetAtPath<GameObject>(TANK);
            var model = tank.GetComponent<VoxelizedModel>();
            const BindingFlags field = BindingFlags.Instance | BindingFlags.NonPublic;
            var camo = (VoxelizedModel.Camouflage)typeof(VoxelizedModel).GetField("_camouflage", field).GetValue(model);
            var decals = (VoxelizedModel.Decal[])typeof(VoxelizedModel).GetField("_decals", field).GetValue(model);
            Assert.IsNotNull(camo.Paint);
            Assert.That(decals.Length, Is.GreaterThan(0));

            var config = ScriptableObject.CreateInstance<VoxelizerConfig>();
            config.kind = VoxelizerConfig.ModelKind.Vehicle;
            config.colours = 12;
            config.parts = new[]
            {
                Rule("HullVisual", VoxelizerConfig.PartMark.Auto),
                Rule("HullVisual/TurretVisual", VoxelizerConfig.PartMark.Auto),
                Rule("HullVisual/TurretVisual/Gun", VoxelizerConfig.PartMark.Auto),
                Rule("HullVisual/Wheel_*", VoxelizerConfig.PartMark.Cylinder),
                Rule("HullVisual/Track?_Front", VoxelizerConfig.PartMark.Auto),
                Rule("HullVisual/Track?_Rear", VoxelizerConfig.PartMark.Auto),
                Rule("HullVisual/Track?_Link*", VoxelizerConfig.PartMark.Skip)
            };
            config.camouflage = camo;
            config.decals = decals;

            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(tank, scene);
                using VoxelizerResult result = VoxelizeUseCase.Run(config, instance);
                int k = config.colours;
                Color32 white = MeshVoxelizer.ToVertexColor(decals[0].Colors[0]);

                foreach (string path in new[] { "HullVisual", "HullVisual/TurretVisual" })
                {
                    VoxelizerResultPart part = result.Parts.Find(p => p.Path == path);
                    Assert.IsNotNull(part, path);
                    var surface = new HashSet<int>();
                    int camouflaged = 0;
                    foreach (byte v in part.Voxels)
                    {
                        if (v == 0 || (v > k && v <= 2 * k)) continue;
                        surface.Add(v);
                        if (v > 2 * k) camouflaged++;
                    }

                    Assert.That(surface.Count, Is.GreaterThan(1), path + ": surface colours");
                    Assert.That(camouflaged, Is.GreaterThan(500), path + ": camouflage voxels");
                }

                VoxelizerResultPart turret = result.Parts.Find(p => p.Path == "HullVisual/TurretVisual");
                int left = 0, right = 0;
                for (int i = 0; i < turret.Voxels.Length; i++)
                {
                    Color32 c = result.Palette[turret.Voxels[i]];
                    if (turret.Voxels[i] == 0 || c.r != white.r || c.g != white.g || c.b != white.b) continue;
                    if (i % turret.Size.x < turret.Size.x / 2) left++; else right++;
                }

                // The number is 31 white pixels a side, the cross 28 more: 118 on the turret in all.
                Assert.That(left, Is.GreaterThanOrEqualTo(40), "decal pixels on the -X side");
                Assert.That(right, Is.GreaterThanOrEqualTo(40), "decal pixels on the +X side");
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
                Object.DestroyImmediate(config);
            }
        }

        private static VoxelizerConfig.PartRule Rule(string path, VoxelizerConfig.PartMark mark) =>
            new() { path = path, mark = mark, axis = 0 };

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
