using System.Collections.Generic;
using Game.Voxels.EditorTools;
using NUnit.Framework;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Voxels.Tests
{
    public sealed class VoxelModelBakeUseCaseTests
    {
        private const string FOLDER = "Assets/__VoxelizerBakeTest";

        [TearDown]
        public void TearDown() => AssetDatabase.DeleteAsset(FOLDER);

        [Test]
        public void Bake_KeepsASkippedPartsRenderersUnderAVoxelizedRoot_AndLeavesTheOpenSceneClean()
        {
            AssetDatabase.DeleteAsset(FOLDER);
            AssetDatabase.CreateFolder("Assets", "__VoxelizerBakeTest");
            var car = new GameObject("Car");
            TestMeshes.Add(car.transform, "Body", TestMeshes.Box(Vector3.one * 0.001f, new Vector3(1.999f, 0.6f, 0.999f)), Color.gray);
            var turret = new GameObject("Turret");
            turret.transform.SetParent(car.transform, false);
            TestMeshes.Add(turret.transform, "Barrel", TestMeshes.Box(new Vector3(0.001f, 0.7f, 0.001f), new Vector3(0.5f, 0.9f, 0.3f)), Color.black);
            // A prefab only keeps meshes and materials that are assets.
            int n = 0;
            foreach (MeshRenderer r in car.GetComponentsInChildren<MeshRenderer>())
            {
                AssetDatabase.CreateAsset(r.GetComponent<MeshFilter>().sharedMesh, $"{FOLDER}/Mesh{n}.asset");
                AssetDatabase.CreateAsset(r.sharedMaterial, $"{FOLDER}/Material{n}.mat");
                n++;
            }

            GameObject model = PrefabUtility.SaveAsPrefabAsset(car, FOLDER + "/Car.prefab");
            Object.DestroyImmediate(car);

            var config = ScriptableObject.CreateInstance<VoxelizerConfig>();
            config.model = model;
            config.kind = VoxelizerConfig.ModelKind.Building;
            config.parts = new[]
            {
                new VoxelizerConfig.PartRule { path = "", mark = VoxelizerConfig.PartMark.Auto },
                new VoxelizerConfig.PartRule { path = "Turret", mark = VoxelizerConfig.PartMark.Skip }
            };
            bool dirtyBefore = SceneManager.GetActiveScene().isDirty;

            GameObject prefab = VoxelModelBakeUseCase.Bake(config, FOLDER);

            Assert.IsNotNull(prefab.GetComponent<VoxelModel>());
            Assert.IsNull(prefab.transform.Find("Body").GetComponent<MeshRenderer>(), "the root's own renderer is voxelized away");
            Transform barrel = prefab.transform.Find("Turret/Barrel");
            Assert.IsNotNull(barrel.GetComponent<MeshRenderer>(), "a skipped part keeps its renderer");
            Assert.IsNotNull(barrel.GetComponent<MeshFilter>());
            Assert.AreEqual(dirtyBefore, SceneManager.GetActiveScene().isDirty, "the open scene is left as it was");
            Object.DestroyImmediate(config);
        }

        /// <summary>The car as a prefab in FOLDER (meshes and materials saved as assets), and a building config for it.</summary>
        private static VoxelizerConfig CarConfig(GameObject car)
        {
            AssetDatabase.DeleteAsset(FOLDER);
            AssetDatabase.CreateFolder("Assets", "__VoxelizerBakeTest");
            int n = 0;
            foreach (MeshRenderer r in car.GetComponentsInChildren<MeshRenderer>(true))
            {
                AssetDatabase.CreateAsset(r.GetComponent<MeshFilter>().sharedMesh, $"{FOLDER}/Mesh{n}.asset");
                AssetDatabase.CreateAsset(r.sharedMaterial, $"{FOLDER}/Material{n}.mat");
                n++;
            }

            GameObject model = PrefabUtility.SaveAsPrefabAsset(car, FOLDER + "/Car.prefab");
            Object.DestroyImmediate(car);
            var config = ScriptableObject.CreateInstance<VoxelizerConfig>();
            config.model = model;
            config.kind = VoxelizerConfig.ModelKind.Building;
            return config;
        }

        private static GameObject Car()
        {
            var car = new GameObject("Car");
            TestMeshes.Add(car.transform, "Body", TestMeshes.Box(Vector3.one * 0.001f, new Vector3(1.999f, 0.6f, 0.999f)), Color.gray);
            return car;
        }

        [Test]
        public void Bake_AModelWithParticles_LeavesTheParticleRendererAlone()
        {
            GameObject car = Car();
            var smoke = new GameObject("Smoke", typeof(ParticleSystem));
            smoke.transform.SetParent(car.transform, false);
            VoxelizerConfig config = CarConfig(car);

            GameObject prefab = VoxelModelBakeUseCase.Bake(config, FOLDER);

            Assert.IsNotNull(prefab.transform.Find("Smoke").GetComponent<ParticleSystemRenderer>());
            Assert.IsNull(prefab.transform.Find("Body").GetComponent<MeshRenderer>());
            Object.DestroyImmediate(config);
        }

        [Test]
        public void Rebake_KeepsTheDataAssetsGuid_AndThePrefabStillPointsAtIt()
        {
            VoxelizerConfig config = CarConfig(Car());
            string path = FOLDER + "/VX_Car_Data.asset";

            VoxelModelBakeUseCase.Bake(config, FOLDER);
            string guid = AssetDatabase.AssetPathToGUID(path);
            config.colours = 6;
            GameObject prefab = VoxelModelBakeUseCase.Bake(config, FOLDER);

            Assert.AreEqual(guid, AssetDatabase.AssetPathToGUID(path));
            var data = AssetDatabase.LoadAssetAtPath<VoxelModelAsset>(path);
            Assert.AreEqual(data, new SerializedObject(prefab.GetComponent<VoxelModel>()).FindProperty("_data").objectReferenceValue);
            Assert.AreEqual(1, data.parts.Length);
            Object.DestroyImmediate(config);
        }

        [Test]
        public void Bake_AScaledRootAndPart_GiveTheClassVoxelInTheWorld()
        {
            var car = new GameObject("Car");
            car.transform.localScale = Vector3.one * 2f;
            var hull = new GameObject("Hull");
            hull.transform.SetParent(car.transform, false);
            hull.transform.localScale = Vector3.one * 2f;
            TestMeshes.Add(hull.transform, "Body", TestMeshes.Box(Vector3.one * 0.001f, new Vector3(0.999f, 0.249f, 0.249f)), Color.gray);
            VoxelizerConfig config = CarConfig(car);
            config.length = 7.984f; // twice its 3.992 m
            config.parts = new[] { new VoxelizerConfig.PartRule { path = "Hull", mark = VoxelizerConfig.PartMark.AsIs } };

            GameObject prefab = VoxelModelBakeUseCase.Bake(config, FOLDER);

            var data = AssetDatabase.LoadAssetAtPath<VoxelModelAsset>(FOLDER + "/VX_Car_Data.asset");
            Assert.AreEqual(0.1f, data.voxelSize, 1e-6f);
            Assert.AreEqual(0.1f, data.parts[0].voxelSize * prefab.transform.Find("Hull").lossyScale.x, 1e-5f);
            Object.DestroyImmediate(config);
        }

        [Test]
        public void ToAsset_KeepsEveryPartAndTheClassSettings()
        {
            var result = new VoxelizerResult { VoxelSize = 0.03f, Palette = new Color32[256] };
            var voxels = new NativeArray<byte>(27, Allocator.Persistent);
            for (int i = 0; i < 27; i++) voxels[i] = 1;
            result.Parts.Add(new VoxelizerResultPart { Path = "Hull", Size = new Unity.Mathematics.int3(3, 3, 3), Voxels = voxels });
            var config = ScriptableObject.CreateInstance<VoxelizerConfig>();
            config.kind = VoxelizerConfig.ModelKind.Vehicle;

            VoxelModelAsset asset = VoxelModelBakeUseCase.ToAsset(result, config);

            Assert.AreEqual(1, asset.parts.Length);
            Assert.AreEqual("Hull", asset.parts[0].path);
            Assert.AreEqual(0.45f, asset.toughness);
            Assert.AreEqual(7800f, asset.density);
            Assert.AreEqual(VoxelAnchor.Largest, asset.anchor);
            Assert.IsFalse(asset.colliders);
            NativeArray<byte> back = VoxelModelAsset.Decode(asset.parts[0].runs, 27, Allocator.Temp);
            Assert.AreEqual(27, System.Linq.Enumerable.Count(back, b => b == 1));
            back.Dispose();
            result.Dispose();
            Object.DestroyImmediate(asset);
            Object.DestroyImmediate(config);
        }
    }
}
