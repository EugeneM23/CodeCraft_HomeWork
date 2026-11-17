using System.Collections.Generic;
using System.Text.RegularExpressions;
using Game.Voxels.EditorTools;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Voxels.Tests
{
    public sealed class VoxelizeUseCaseTests
    {
        [Test]
        public void AHullWithAWheel_GivesTwoParts_TheWheelARoundCylinder_ASkippedPartNothing()
        {
            var root = new GameObject("Car");
            var hull = new GameObject("Hull");
            hull.transform.SetParent(root.transform, false);
            TestMeshes.Add(hull.transform, "Body", TestMeshes.Box(Vector3.one * 0.001f, new Vector3(1.999f, 0.6f, 0.999f)), Color.gray);
            var wheel = new GameObject("Wheel_1");
            wheel.transform.SetParent(hull.transform, false);
            wheel.transform.localPosition = new Vector3(2.2f, 0f, 0f);
            TestMeshes.Add(wheel.transform, "Tyre", TestMeshes.Box(new Vector3(0.001f, -0.399f, -0.399f), new Vector3(0.299f, 0.399f, 0.399f)), Color.black);
            var aerial = new GameObject("Aerial");
            aerial.transform.SetParent(hull.transform, false);
            TestMeshes.Add(aerial.transform, "Rod", TestMeshes.Box(Vector3.one * 0.001f, new Vector3(0.05f, 1f, 0.05f)), Color.black);

            var config = ScriptableObject.CreateInstance<VoxelizerConfig>();
            config.kind = VoxelizerConfig.ModelKind.Building; // 0.1 m, keeps the test grid small
            config.parts = new[]
            {
                new VoxelizerConfig.PartRule { path = "Hull", mark = VoxelizerConfig.PartMark.Auto },
                new VoxelizerConfig.PartRule { path = "Hull/Wheel_*", mark = VoxelizerConfig.PartMark.Cylinder, axis = 0 },
                new VoxelizerConfig.PartRule { path = "Hull/Aerial", mark = VoxelizerConfig.PartMark.Skip }
            };

            using VoxelizerResult result = VoxelizeUseCase.Run(config, root);

            Assert.AreEqual(2, result.Parts.Count);
            VoxelizerResultPart wheelPart = result.Parts.Find(p => p.Path == "Hull/Wheel_1");
            Assert.IsNotNull(wheelPart);
            int solid = 0;
            foreach (byte b in wheelPart.Voxels) if (b != 0) solid++;
            Assert.AreEqual(3 * 52, solid); // 8 voxels across: 52 cells a disc, 3 thick

            Object.DestroyImmediate(root);
            Object.DestroyImmediate(config);
        }

        private static int RunPlate(bool cutThinDetails)
        {
            var root = new GameObject("Plate");
            TestMeshes.Add(root.transform, "Plate", TestMeshes.Box(Vector3.one * 0.001f, new Vector3(1.999f, 0.099f, 1.999f)), Color.gray);
            var config = ScriptableObject.CreateInstance<VoxelizerConfig>();
            config.kind = VoxelizerConfig.ModelKind.Building; // 0.1 m: the plate is one voxel thick
            config.cutThinDetails = cutThinDetails;

            using VoxelizerResult result = VoxelizeUseCase.Run(config, root);

            int solid = 0;
            foreach (VoxelizerResultPart part in result.Parts)
                foreach (byte b in part.Voxels) if (b != 0) solid++;
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(config);
            return solid;
        }

        [Test]
        public void AOneVoxelPlate_UnderAuto_StaysByDefault()
        {
            Assert.Greater(RunPlate(false), 0);
        }

        [Test]
        public void AOneVoxelPlate_UnderAuto_VanishesWithCutThinDetails()
        {
            Assert.AreEqual(0, RunPlate(true));
        }

        [Test]
        public void VoxelSizeFor_Vehicle_IsThreeCmUpToSixMetres_SixCmBeyond_BuildingTenCm()
        {
            var config = ScriptableObject.CreateInstance<VoxelizerConfig>();
            config.kind = VoxelizerConfig.ModelKind.Vehicle;
            Assert.AreEqual(0.03f, config.VoxelSizeFor(5f), 1e-6f);
            Assert.AreEqual(0.03f, config.VoxelSizeFor(6f), 1e-6f);
            Assert.AreEqual(0.06f, config.VoxelSizeFor(7f), 1e-6f);
            config.kind = VoxelizerConfig.ModelKind.Building;
            Assert.AreEqual(0.1f, config.VoxelSizeFor(5f), 1e-6f);
            Assert.AreEqual(0.1f, config.VoxelSizeFor(50f), 1e-6f);
            Object.DestroyImmediate(config);
        }

        [Test]
        public void Run_ALongVehicle_UsesSixCm_OverTheScale()
        {
            var root = new GameObject("Truck");
            TestMeshes.Add(root.transform, "Body", TestMeshes.Box(Vector3.one * 0.001f, new Vector3(0.999f, 0.3f, 0.3f)), Color.gray);
            var config = ScriptableObject.CreateInstance<VoxelizerConfig>();
            config.kind = VoxelizerConfig.ModelKind.Vehicle;
            config.length = 8f; // a 1 m model stands for 8 m: scale 8

            using VoxelizerResult result = VoxelizeUseCase.Run(config, root);

            Assert.AreEqual(8f / 0.998f, result.Scale, 1e-3f); // the mesh runs 0.001..0.999: 0.998 m (the pivot does not count)
            Assert.AreEqual(0.06f / result.Scale, result.VoxelSize, 1e-6f);
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(config);
        }

        [Test]
        public void Run_AVehicleWithNoLength_TakesTheModelsLongestSideAsTheRealLength()
        {
            var root = new GameObject("Truck");
            TestMeshes.Add(root.transform, "Body", TestMeshes.Box(Vector3.one * 0.001f, new Vector3(7.999f, 0.3f, 0.3f)), Color.gray);
            var config = ScriptableObject.CreateInstance<VoxelizerConfig>();
            config.kind = VoxelizerConfig.ModelKind.Vehicle;

            using VoxelizerResult result = VoxelizeUseCase.Run(config, root);

            Assert.AreEqual(0.06f, result.VoxelSize, 1e-6f);
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(config);
        }

        /// <summary>How many x columns of the part's grid hold a voxel.</summary>
        private static int XExtent(VoxelizerResultPart part)
        {
            var used = new bool[part.Size.x];
            for (int i = 0; i < part.Voxels.Length; i++)
                if (part.Voxels[i] != 0) used[i % part.Size.x] = true;
            int n = 0;
            foreach (bool u in used) if (u) n++;
            return n;
        }

        [TestCase(0f, 40)]      // as the model is: 4 m in the world at 10 cm
        [TestCase(7.984f, 80)]  // twice the world length (3.992 m): 8 m at 10 cm
        public void APartScaledTwice_UnderARootScaledTwice_GetsTheClassVoxelInTheWorld(float length, int columns)
        {
            var root = new GameObject("Car");
            root.transform.localScale = Vector3.one * 2f;
            var hull = new GameObject("Hull");
            hull.transform.SetParent(root.transform, false);
            hull.transform.localScale = Vector3.one * 2f;
            // 0.998 m in the hull's space: 3.992 m in the world.
            TestMeshes.Add(hull.transform, "Body", TestMeshes.Box(Vector3.one * 0.001f, new Vector3(0.999f, 0.249f, 0.249f)), Color.gray);
            var config = ScriptableObject.CreateInstance<VoxelizerConfig>();
            config.kind = VoxelizerConfig.ModelKind.Building; // 0.1 m
            config.length = length;
            config.parts = new[] { new VoxelizerConfig.PartRule { path = "Hull", mark = VoxelizerConfig.PartMark.AsIs } };

            using VoxelizerResult result = VoxelizeUseCase.Run(config, root);

            Assert.AreEqual(1, result.Parts.Count);
            VoxelizerResultPart part = result.Parts[0];
            Assert.AreEqual(columns, XExtent(part));
            Assert.AreEqual(0.1f, result.ClassVoxel, 1e-6f);
            // Part space × the part's world scale × Scale (the bake's root scale) = real metres.
            Assert.AreEqual(0.1f, part.VoxelSize * 4f * result.Scale, 1e-5f);
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(config);
        }

        [TestCase("")]
        [TestCase("Hull")]
        public void ANonUniformScale_OnTheRootOrAPart_IsRefused(string scaled)
        {
            var root = new GameObject("Car");
            var hull = new GameObject("Hull");
            hull.transform.SetParent(root.transform, false);
            TestMeshes.Add(hull.transform, "Body", TestMeshes.Box(Vector3.one * 0.001f, new Vector3(0.999f, 0.249f, 0.249f)), Color.gray);
            (scaled == "" ? root : hull).transform.localScale = new Vector3(1f, 2f, 1f);
            var config = ScriptableObject.CreateInstance<VoxelizerConfig>();
            config.kind = VoxelizerConfig.ModelKind.Building;
            config.parts = new[] { new VoxelizerConfig.PartRule { path = "Hull", mark = VoxelizerConfig.PartMark.AsIs } };

            var e = Assert.Catch<System.InvalidOperationException>(() => VoxelizeUseCase.Run(config, root).Dispose());
            StringAssert.Contains("non-uniform", e.Message);
            StringAssert.Contains(scaled == "" ? "Car" : "Hull", e.Message);
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(config);
        }

        [Test]
        public void TheRealLength_ComesFromActiveMeshesOnly_NotThePivotAnInactiveChildOrParticles()
        {
            var root = new GameObject("Truck"); // the pivot is 50 m away from the mesh
            MeshRenderer body = TestMeshes.Add(root.transform, "Body", TestMeshes.Box(Vector3.one * 0.001f, new Vector3(1.999f, 0.5f, 0.5f)), Color.gray);
            body.transform.localPosition = new Vector3(50f, 0f, 0f);
            MeshRenderer hidden = TestMeshes.Add(root.transform, "Hidden", TestMeshes.Box(Vector3.one * 0.001f, new Vector3(9.999f, 0.5f, 0.5f)), Color.red);
            hidden.gameObject.SetActive(false);
            var smoke = new GameObject("Smoke", typeof(ParticleSystem));
            smoke.transform.SetParent(root.transform, false);
            smoke.transform.localPosition = new Vector3(-30f, 0f, 0f);
            var config = ScriptableObject.CreateInstance<VoxelizerConfig>();
            config.kind = VoxelizerConfig.ModelKind.Vehicle;

            using (VoxelizerResult result = VoxelizeUseCase.Run(config, root))
            {
                Assert.AreEqual(0.03f, result.VoxelSize, 1e-6f, "1.998 m long: a 3 cm vehicle");
                Assert.AreEqual(1, result.Parts.Count);
                Assert.LessOrEqual(XExtent(result.Parts[0]), 68, "the hidden box is not voxelized");
            }

            config.length = 4f;
            using (VoxelizerResult result = VoxelizeUseCase.Run(config, root))
                Assert.AreEqual(4f / 1.998f, result.Scale, 1e-3f);
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(config);
        }

        [Test]
        public void APartOver512CellsOnAnAxis_IsRefusedWithAHint_NotTruncated()
        {
            var root = new GameObject("Barge");
            TestMeshes.Add(root.transform, "Hull", TestMeshes.Box(Vector3.one * 0.001f, new Vector3(99.999f, 1f, 1f)), Color.gray);
            var config = ScriptableObject.CreateInstance<VoxelizerConfig>();
            config.kind = VoxelizerConfig.ModelKind.Vehicle; // no length: 100 m at 6 cm, 1 667 cells

            var e = Assert.Catch<System.InvalidOperationException>(() => VoxelizeUseCase.Run(config, root).Dispose());
            StringAssert.Contains("512", e.Message);
            StringAssert.Contains("Length", e.Message);
            StringAssert.Contains("(root)", e.Message);
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(config);
        }

        [Test]
        public void ATransformTwoRulesMatch_TakesTheFirstRule_WithAWarning_AndARuleMatchingNothingWarns()
        {
            var root = new GameObject("Car");
            var hull = new GameObject("Hull");
            hull.transform.SetParent(root.transform, false);
            TestMeshes.Add(hull.transform, "Body", TestMeshes.Box(Vector3.one * 0.001f, new Vector3(1.999f, 0.6f, 0.999f)), Color.gray);
            var wheel = new GameObject("Wheel_1");
            wheel.transform.SetParent(hull.transform, false);
            wheel.transform.localPosition = new Vector3(2.2f, 0f, 0f);
            TestMeshes.Add(wheel.transform, "Tyre", TestMeshes.Box(new Vector3(0.001f, -0.399f, -0.399f), new Vector3(0.299f, 0.399f, 0.399f)), Color.black);
            var config = ScriptableObject.CreateInstance<VoxelizerConfig>();
            config.kind = VoxelizerConfig.ModelKind.Building;
            config.parts = new[]
            {
                new VoxelizerConfig.PartRule { path = "Hull", mark = VoxelizerConfig.PartMark.Auto },
                new VoxelizerConfig.PartRule { path = "Hull/Wheel_*", mark = VoxelizerConfig.PartMark.Cylinder, axis = 0 },
                new VoxelizerConfig.PartRule { path = "Hull/Wheel_1", mark = VoxelizerConfig.PartMark.Box },
                new VoxelizerConfig.PartRule { path = "Hull/Turret", mark = VoxelizerConfig.PartMark.Auto }
            };
            LogAssert.Expect(LogType.Warning, new Regex("Hull/Wheel_1.*already"));
            LogAssert.Expect(LogType.Warning, new Regex("Hull/Turret.*matches nothing"));

            using VoxelizerResult result = VoxelizeUseCase.Run(config, root);

            List<VoxelizerResultPart> wheels = result.Parts.FindAll(p => p.Path == "Hull/Wheel_1");
            Assert.AreEqual(1, wheels.Count);
            int solid = 0;
            foreach (byte b in wheels[0].Voxels) if (b != 0) solid++;
            Assert.AreEqual(3 * 52, solid, "the cylinder rule, the first to match, is the one used");
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(config);
        }
    }
}
