using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

namespace Game.Horde.Tests
{
    public class HordePathfinderTests
    {
        /// <summary>Samples every segment of the path every 5 cm; no sample may land on a cell higher than the floor.</summary>
        private static void AssertClear(HordeNavGrid grid, NativeList<float3> path)
        {
            for (int i = 1; i < path.Length; i++)
            {
                float3 a = path[i - 1];
                float3 b = path[i];
                int samples = (int)math.ceil(math.distance(a.xz, b.xz) / 0.05f);
                for (int s = 0; s <= samples; s++)
                {
                    float3 p = math.lerp(a, b, s / (float)math.max(1, samples));
                    Assert.IsTrue(grid.TryCell(p, out int2 cell));
                    Assert.Less(grid.HeightOf(cell), 1f, "segment " + i + " crosses the wall at " + cell);
                }
            }
        }

        [Test]
        public void PathAvoidsWall_AndIsSimplified()
        {
            using HordeNavGrid grid = HordeTestGrids.Flat(30, 30);
            HordeTestGrids.Block(grid, new int2(15, 0), new int2(15, 24));
            using var finder = new HordePathfinder(grid);
            var start = new float3(5.5f, 0f, 5.5f);
            var goal = new float3(25.5f, 0f, 5.5f);

            Assert.AreEqual(HordePathStatus.Found, finder.FindPath(start, goal));
            NativeList<float3> path = finder.Path;
            Assert.GreaterOrEqual(path.Length, 3, "has to bend round the wall end");
            Assert.LessOrEqual(path.Length, 5, "string pulling drops the cell-by-cell zigzag");
            Assert.AreEqual(start.xz, path[0].xz);
            Assert.AreEqual(goal.xz, path[path.Length - 1].xz);
            AssertClear(grid, path);

            bool passedEnd = false;
            for (int i = 0; i < path.Length; i++)
                passedEnd |= path[i].z > 24f;
            Assert.IsTrue(passedEnd, "goes round the far end of the wall");
        }

        [Test]
        public void OpenGround_IsOneStraightSegment_WithHeights()
        {
            using HordeNavGrid grid = HordeTestGrids.Flat(20, 20);
            for (int z = 0; z < 20; z++)
            for (int x = 0; x < 20; x++)
                grid.SetHeight(new int2(x, z), x * 0.1f);
            using var finder = new HordePathfinder(grid);

            Assert.AreEqual(HordePathStatus.Found, finder.FindPath(new float3(1.5f, 0f, 2.5f), new float3(18.5f, 0f, 13.5f)));
            Assert.AreEqual(2, finder.Path.Length);
            Assert.AreEqual(0.1f, finder.Path[0].y, 1e-5f);
            Assert.AreEqual(1.8f, finder.Path[1].y, 1e-5f);
        }

        [Test]
        public void UnreachableGoal_GivesPartialPathToClosestCell()
        {
            using HordeNavGrid grid = HordeTestGrids.Flat(20, 20);
            HordeTestGrids.Block(grid, new int2(12, 0), new int2(12, 19)); // full wall
            using var finder = new HordePathfinder(grid);

            Assert.AreEqual(HordePathStatus.Partial, finder.FindPath(new float3(2.5f, 0f, 10.5f), new float3(17.5f, 0f, 10.5f)));
            float3 last = finder.Path[finder.Path.Length - 1];
            Assert.AreEqual(11.5f, last.x, 1e-5f, "stops at the wall, as close as it gets");
            Assert.AreEqual(10.5f, last.z, 1e-5f);
            AssertClear(grid, finder.Path);
        }

        [Test]
        public void StartWithoutGround_Fails()
        {
            using HordeNavGrid grid = HordeTestGrids.Flat(5, 5);
            grid.SetHeight(new int2(0, 0), HordeNavGrid.NO_GROUND);
            using var finder = new HordePathfinder(grid);
            Assert.AreEqual(HordePathStatus.Failed, finder.FindPath(new float3(0.5f, 0f, 0.5f), new float3(4.5f, 0f, 4.5f)));
            Assert.AreEqual(0, finder.Path.Length);
        }

        [Test]
        public void RepeatedSearches_ReuseBuffers()
        {
            using HordeNavGrid grid = HordeTestGrids.Flat(16, 16);
            HordeTestGrids.Block(grid, new int2(8, 0), new int2(8, 12));
            using var finder = new HordePathfinder(grid);
            for (int i = 0; i < 5; i++)
            {
                Assert.AreEqual(HordePathStatus.Found, finder.FindPath(new float3(2.5f, 0f, 2.5f), new float3(13.5f, 0f, 2.5f)));
                AssertClear(grid, finder.Path);
            }

            HordeTestGrids.Block(grid, new int2(8, 13), new int2(8, 15));
            Assert.AreEqual(HordePathStatus.Partial, finder.FindPath(new float3(2.5f, 0f, 2.5f), new float3(13.5f, 0f, 2.5f)),
                "picks up the grid change");
        }
    }
}
