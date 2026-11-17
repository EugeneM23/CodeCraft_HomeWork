using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Horde.Tests
{
    public class HordeNavGridTests
    {
        [Test]
        public void StepHigherThanMaxStep_Blocks_LowerStepPasses()
        {
            using HordeNavGrid grid = HordeTestGrids.Flat(4, 1);
            grid.SetHeight(new int2(1, 0), 0.7f);
            grid.SetHeight(new int2(3, 0), 0.5f);

            Assert.IsFalse(grid.CanStep(new int2(0, 0), new int2(1, 0)), "0.7 m up is above MaxStep 0.6");
            Assert.IsFalse(grid.CanStep(new int2(1, 0), new int2(0, 0)), "and down too");
            Assert.IsTrue(grid.CanStep(new int2(2, 0), new int2(3, 0)), "0.5 m is climbable");
        }

        [Test]
        public void GentleSlope_Passes_Cliff_DoesNot()
        {
            using HordeNavGrid grid = HordeTestGrids.Flat(10, 2);
            for (int x = 0; x < 10; x++)
            {
                grid.SetHeight(new int2(x, 0), x * 0.4f);   // ramp, 0.4 m per cell
                grid.SetHeight(new int2(x, 1), x < 5 ? 0f : 3.6f); // cliff halfway
            }

            for (int x = 0; x < 9; x++)
                Assert.IsTrue(grid.CanStep(new int2(x, 0), new int2(x + 1, 0)), "ramp step " + x);
            Assert.IsFalse(grid.CanStep(new int2(4, 1), new int2(5, 1)));
        }

        [Test]
        public void NoGround_IsNeverSteppable()
        {
            using HordeNavGrid grid = HordeTestGrids.Flat(3, 1);
            grid.SetHeight(new int2(1, 0), HordeNavGrid.NO_GROUND);

            Assert.IsFalse(grid.HasGround(new int2(1, 0)));
            Assert.IsFalse(grid.CanStep(new int2(0, 0), new int2(1, 0)));
            Assert.IsFalse(grid.CanStep(new int2(1, 0), new int2(2, 0)));
            Assert.IsTrue(float.IsNaN(grid.HeightAt(new float3(1.5f, 0f, 0.5f))));
        }

        [Test]
        public void Diagonal_NeedsBothSides()
        {
            using HordeNavGrid grid = HordeTestGrids.Flat(3, 3);
            Assert.IsTrue(grid.CanStep(new int2(0, 0), new int2(1, 1)));

            grid.SetHeight(new int2(1, 0), 5f);
            Assert.IsFalse(grid.CanStep(new int2(0, 0), new int2(1, 1)), "corner of a wall cuts the diagonal");
            Assert.IsFalse(grid.CanStep(new int2(1, 1), new int2(0, 0)));
            Assert.IsFalse(grid.CanStep(new int2(0, 0), new int2(2, 2)), "not neighbours");
        }

        [Test]
        public void TryCell_And_HeightAt()
        {
            using HordeNavGrid grid = HordeTestGrids.Flat(4, 4);
            for (int z = 0; z < 4; z++)
            for (int x = 0; x < 4; x++)
                grid.SetHeight(new int2(x, z), x * 0.2f);
            grid.SetHeight(new int2(3, 1), 5f);

            Assert.IsTrue(grid.TryCell(new float3(2.3f, 0f, 1.9f), out int2 cell));
            Assert.AreEqual(new int2(2, 1), cell);
            Assert.IsFalse(grid.TryCell(new float3(-0.1f, 0f, 1f), out _));
            Assert.IsFalse(grid.TryCell(new float3(4.1f, 0f, 1f), out _));

            Assert.AreEqual(0.3f, grid.HeightAt(new float3(2f, 0f, 2.5f)), 1e-4f, "bilinear between 0.2 and 0.4");
            Assert.AreEqual(0.4f, grid.HeightAt(new float3(2.9f, 0f, 1.5f)), 1e-4f, "the wall next door does not lift the floor");
        }

        [Test]
        public void MarkDirty_MergesOverlappingAndTouchingRects()
        {
            using HordeNavGrid grid = HordeTestGrids.Flat(20, 20);

            grid.MarkDirty(new Bounds(new Vector3(3f, 0f, 3f), new Vector3(1.8f, 1f, 1.8f)));
            Assert.AreEqual(1, grid.DirtyRectCount);
            Assert.AreEqual(new int4(1, 1, 4, 4), grid.GetDirtyRect(0), "cells 2..3 padded by one");

            grid.MarkDirty(new Bounds(new Vector3(5f, 0f, 5f), new Vector3(1.8f, 1f, 1.8f)));
            Assert.AreEqual(1, grid.DirtyRectCount, "overlapping rects merge");
            Assert.AreEqual(new int4(1, 1, 6, 6), grid.GetDirtyRect(0));

            grid.MarkDirty(new Bounds(new Vector3(15.5f, 0f, 15.5f), new Vector3(0.5f, 1f, 0.5f)));
            Assert.AreEqual(2, grid.DirtyRectCount, "far rect stays apart");

            grid.MarkDirty(new Bounds(new Vector3(10.5f, 0f, 10.5f), new Vector3(7f, 1f, 7f)));
            Assert.AreEqual(1, grid.DirtyRectCount, "a rect touching both swallows them");
            Assert.AreEqual(new int4(1, 1, 16, 16), grid.GetDirtyRect(0));

            grid.MarkDirty(new Bounds(new Vector3(100f, 0f, 100f), Vector3.one));
            Assert.AreEqual(1, grid.DirtyRectCount, "outside the grid adds nothing");

            grid.MarkDirty(new Bounds(new Vector3(-0.5f, 0f, 19.5f), Vector3.one));
            Assert.AreEqual(2, grid.DirtyRectCount);
            Assert.AreEqual(new int4(0, 18, 1, 19), grid.GetDirtyRect(1), "clamped to the grid edge");
        }

        [Test]
        public void Version_BumpsOnHeightChange()
        {
            using HordeNavGrid grid = HordeTestGrids.Flat(2, 2);
            int before = grid.Version;
            grid.SetHeight(new int2(1, 1), 2f);
            Assert.Greater(grid.Version, before);
        }
    }
}
