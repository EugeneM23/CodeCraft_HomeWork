using NUnit.Framework;
using Unity.Mathematics;

namespace Game.Horde.Tests
{
    public class HordeFlowFieldTests
    {
        private static void Compute(HordeFlowField field, int2 target)
        {
            field.Schedule(HordeTestGrids.Center(target));
            field.Complete();
        }

        /// <summary>Follows the field cell by cell; returns the steps taken, or -1 if it got stuck.</summary>
        private static int Walk(HordeNavGrid grid, HordeFlowField field, int2 start, int maxSteps)
        {
            int2 cell = start;
            for (int step = 0; step < maxSteps; step++)
            {
                if (math.all(cell == field.TargetCell))
                    return step;
                float2 dir = field.DirectionOf(cell);
                if (math.lengthsq(dir) < 0.5f)
                    return -1;
                int2 next = cell + (int2)math.round(dir);
                Assert.IsTrue(grid.CanStep(cell, next), "field points through a blocked step at " + cell);
                cell = next;
            }

            return -1;
        }

        [Test]
        public void LeadsAroundTallWall()
        {
            using HordeNavGrid grid = HordeTestGrids.Flat(20, 20);
            HordeTestGrids.Block(grid, new int2(10, 0), new int2(10, 14));
            using var field = new HordeFlowField(grid);
            Compute(field, new int2(15, 2));

            int steps = Walk(grid, field, new int2(5, 2), 100);
            Assert.Greater(steps, 0, "reaches the target");
            Assert.Greater(steps, 10, "goes round, not through");
            Assert.Greater(field.DistanceOf(new int2(5, 2)), 25f, "the way round the wall end is long");
            Assert.AreEqual(0f, field.DistanceOf(new int2(15, 2)));
            Assert.IsTrue(field.IsReached(HordeTestGrids.Center(new int2(15, 2))));
            Assert.IsTrue(field.IsUnreachable(new int2(10, 5)), "the wall top itself is cut off");
        }

        [Test]
        public void NoDiagonalCornerCutting()
        {
            using HordeNavGrid grid = HordeTestGrids.Flat(3, 3);
            grid.SetHeight(new int2(1, 0), 5f);
            using var field = new HordeFlowField(grid);
            Compute(field, new int2(1, 1));

            Assert.AreEqual(2f, field.DistanceOf(new int2(0, 0)), 1e-5f, "round the corner: two straight steps, not one diagonal");
            Assert.AreEqual(new float2(0f, 1f), field.DirectionOf(new int2(0, 0)));

            grid.SetHeight(new int2(0, 1), 5f);
            Compute(field, new int2(1, 1));
            Assert.IsTrue(field.IsUnreachable(new int2(0, 0)), "boxed in by two corners");
        }

        [Test]
        public void EnclosedAndGroundlessCells_AreUnreachable()
        {
            using HordeNavGrid grid = HordeTestGrids.Flat(12, 12);
            // A ring of wall round cells 4..6; the target is outside it.
            HordeTestGrids.Block(grid, new int2(3, 3), new int2(7, 3));
            HordeTestGrids.Block(grid, new int2(3, 7), new int2(7, 7));
            HordeTestGrids.Block(grid, new int2(3, 4), new int2(3, 6));
            HordeTestGrids.Block(grid, new int2(7, 4), new int2(7, 6));
            grid.SetHeight(new int2(10, 10), HordeNavGrid.NO_GROUND);
            using var field = new HordeFlowField(grid);
            Compute(field, new int2(0, 0));

            Assert.IsTrue(field.IsUnreachable(new int2(5, 5)));
            Assert.IsTrue(float.IsPositiveInfinity(field.DistanceOf(new int2(5, 5))));
            Assert.AreEqual(float2.zero, field.DirectionOf(new int2(5, 5)));
            Assert.IsTrue(field.IsUnreachable(new int2(10, 10)), "a hole");
            Assert.IsFalse(field.IsUnreachable(new int2(11, 11)));
            Assert.Greater(Walk(grid, field, new int2(11, 11), 100), 0);
        }

        [Test]
        public void GentleSlope_IsWalked_StepIsNot()
        {
            using HordeNavGrid grid = HordeTestGrids.Flat(10, 3);
            for (int x = 0; x < 10; x++)
            {
                grid.SetHeight(new int2(x, 0), x * 0.5f);
                grid.SetHeight(new int2(x, 1), 9f);
                grid.SetHeight(new int2(x, 2), x < 5 ? 0f : 4.5f);
            }

            using var field = new HordeFlowField(grid);
            Compute(field, new int2(9, 0));
            Assert.Greater(Walk(grid, field, new int2(0, 0), 20), 0, "up the slope");

            Compute(field, new int2(9, 2));
            Assert.IsTrue(field.IsUnreachable(new int2(0, 2)), "4.5 m step");
        }

        [Test]
        public void PreviousField_StaysReadable_UntilComplete()
        {
            using HordeNavGrid grid = HordeTestGrids.Flat(8, 8);
            using var field = new HordeFlowField(grid);
            Compute(field, new int2(0, 0));
            Assert.AreEqual(grid.Version, field.GridVersion);

            HordeTestGrids.Block(grid, new int2(2, 0), new int2(2, 6));
            field.Schedule(HordeTestGrids.Center(new int2(7, 7)));
            Assert.IsTrue(field.IsRunning);
            Assert.AreEqual(new int2(0, 0), field.TargetCell);
            Assert.AreEqual(new float2(-1f, 0f), field.DirectionOf(new int2(1, 0)), "old field still answers");

            Assert.IsTrue(field.Complete());
            Assert.AreEqual(new int2(7, 7), field.TargetCell);
            Assert.AreEqual(grid.Version, field.GridVersion, "snapshot picked up the new wall");
            Assert.IsTrue(field.IsUnreachable(new int2(2, 3)));
        }
    }
}
