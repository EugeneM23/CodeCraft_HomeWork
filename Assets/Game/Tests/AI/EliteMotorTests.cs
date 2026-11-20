using Game.Horde;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Game.AIKit.Adapters.Tests
{
    /// <summary>The elite's body against the grid: a long frame's big step does not carry it through a thin wall.</summary>
    public sealed class EliteMotorTests
    {
        private HordeNavGrid _grid;
        private LevelNav _nav;
        private GameObject _body;
        private EliteMotor _motor;

        [SetUp]
        public void SetUp()
        {
            _grid = new HordeNavGrid(HordeNavGridSettings.Cover(new float2(-10f, -10f), new float2(10f, 10f), 0.5f));
            using (var heights = new NativeArray<float>(_grid.CellCount, Allocator.Temp))
                _grid.SetHeights(heights);
            // A wall one cell thick along Z at x = 0..0.5.
            int2 a = _grid.ClampCell(new float3(0.1f, 0f, -10f));
            int2 b = _grid.ClampCell(new float3(0.1f, 0f, 9.9f));
            for (int z = a.y; z <= b.y; z++)
                _grid.SetHeight(new int2(a.x, z), 4f);

            _nav = new LevelNav(_grid);
            _body = new GameObject("EliteMotorTests");
            _body.transform.position = new Vector3(-0.6f, 0f, 0f);
            _motor = new EliteMotor(_body.transform);
            _motor.Bind(_nav, null);
            _motor.Snap();
        }

        [TearDown]
        public void TearDown()
        {
            _motor.Release();
            Object.DestroyImmediate(_body);
            _nav.Dispose();
            _grid.Dispose();
        }

        [Test]
        public void ABigStepAcrossAThinWall_IsTakenBack()
        {
            _body.transform.position = new Vector3(0.9f, 0f, 0f); // 1.5 m in one tick: three cells, the wall between

            _motor.Tick(0.2f);

            Assert.Less(_body.transform.position.x, 0f, "still on its side of the wall");
        }

        [Test]
        public void ABigStepOnOpenGround_IsWalked()
        {
            _body.transform.position = new Vector3(-0.6f, 0f, 1.5f);

            _motor.Tick(0.2f);

            Assert.AreEqual(1.5f, _body.transform.position.z, 1e-4f);
        }

        [Test]
        public void APlacementFarAway_IsNotTakenBack()
        {
            _body.transform.position = new Vector3(5f, 0f, 0f); // a teleport over the wall

            _motor.Tick(0.2f);

            Assert.AreEqual(5f, _body.transform.position.x, 1e-4f);
        }
    }
}
