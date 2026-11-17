using Game.Voxels.EditorTools;
using NUnit.Framework;
using UnityEngine;

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
    }
}
