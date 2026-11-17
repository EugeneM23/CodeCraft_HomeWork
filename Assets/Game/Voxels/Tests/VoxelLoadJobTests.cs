using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Game.Voxels.Tests
{
    /// <summary>The load solve and the seam cells on hand-made block graphs.</summary>
    public sealed class VoxelLoadJobTests
    {
        private const float STEP = 0.4f;
        private static readonly float2 Strong = new(16000f, 160f);

        private sealed class Graph : System.IDisposable
        {
            public readonly int3 Blocks;
            public NativeArray<float> Mass;
            public NativeArray<float2> BondX, BondY, BondZ;
            public NativeArray<byte> Support;

            public Graph(int3 blocks)
            {
                Blocks = blocks;
                int count = blocks.x * blocks.y * blocks.z;
                Mass = new NativeArray<float>(count, Allocator.TempJob);
                BondX = new NativeArray<float2>(count, Allocator.TempJob);
                BondY = new NativeArray<float2>(count, Allocator.TempJob);
                BondZ = new NativeArray<float2>(count, Allocator.TempJob);
                Support = new NativeArray<byte>(blocks.x * blocks.z, Allocator.TempJob);
            }

            public int At(int x, int y, int z = 0) => x + Blocks.x * (y + Blocks.y * z);

            /// <summary>Islands per block after the solve, and how many.</summary>
            public int[] Solve(out int islands)
            {
                int count = Mass.Length;
                var extra = new NativeArray<float>(count, Allocator.TempJob);
                var broken = new NativeArray<byte>(count * 3, Allocator.TempJob);
                var scale = new NativeArray<float>(count * 3, Allocator.TempJob);
                for (int i = 0; i < scale.Length; i++)
                    scale[i] = 1f;
                var island = new NativeArray<int>(count, Allocator.TempJob);
                var result = new NativeArray<int>(5, Allocator.TempJob);
                new StructuralLoadJob
                {
                    ZoneLo = new int3(-1), ZoneHi = Blocks,
                    Blocks = Blocks, Mass = Mass, BondX = BondX, BondY = BondY, BondZ = BondZ, Support = Support,
                    Extra = extra, Step = STEP, Broken = broken, Scale = scale, Island = island, Result = result
                }.Run();
                islands = result[0];
                int[] copy = island.ToArray();
                extra.Dispose();
                broken.Dispose();
                scale.Dispose();
                island.Dispose();
                result.Dispose();
                return copy;
            }

            public void Dispose()
            {
                Mass.Dispose();
                BondX.Dispose();
                BondY.Dispose();
                BondZ.Dispose();
                Support.Dispose();
            }
        }

        [Test]
        public void Column_StrongJoints_Holds()
        {
            using var g = new Graph(new int3(1, 3, 1));
            for (int y = 0; y < 3; y++)
                g.Mass[g.At(0, y)] = 100f;
            g.BondY[g.At(0, 0)] = Strong;
            g.BondY[g.At(0, 1)] = Strong;
            g.Support[0] = 1;

            g.Solve(out int islands);
            Assert.AreEqual(0, islands);
        }

        [Test]
        public void Column_ThinNeck_IsCrushed_AndTheTopFallsWhole()
        {
            using var g = new Graph(new int3(1, 3, 1));
            for (int y = 0; y < 3; y++)
                g.Mass[g.At(0, y)] = 100f;
            // The neck bears 150 kg; 200 rest on it.
            g.BondY[g.At(0, 0)] = new float2(150f, 10f);
            g.BondY[g.At(0, 1)] = Strong;
            g.Support[0] = 1;

            int[] island = g.Solve(out int islands);
            // The neck gave way pressed: the block under it is crushed (falls on its own), the top falls whole.
            Assert.AreEqual(2, islands);
            Assert.AreNotEqual(0, island[g.At(0, 0)]);
            Assert.AreNotEqual(0, island[g.At(0, 1)]);
            Assert.AreEqual(island[g.At(0, 1)], island[g.At(0, 2)]);
            Assert.AreNotEqual(island[g.At(0, 0)], island[g.At(0, 1)]);
        }

        [Test]
        public void Ground_Gone_EverythingFalls()
        {
            using var g = new Graph(new int3(1, 2, 1));
            g.Mass[g.At(0, 0)] = 100f;
            g.Mass[g.At(0, 1)] = 100f;
            g.BondY[g.At(0, 0)] = Strong;

            g.Solve(out int islands);
            Assert.AreEqual(1, islands, "no support: the whole column is one island");
        }

        /// <summary>A post with an arm of five 100 kg blocks: 600 kg·m at the root.</summary>
        [TestCase(1000f, 0)]
        [TestCase(300f, 1)]
        public void Cantilever_BreaksAtTheRoot_WhenTooLong(float bend, int expected)
        {
            using var g = new Graph(new int3(6, 2, 1));
            g.Mass[g.At(0, 0)] = 100f;
            g.BondY[g.At(0, 0)] = Strong;
            g.Support[0] = 1;
            for (int x = 0; x < 6; x++)
                g.Mass[g.At(x, 1)] = 100f;
            for (int x = 0; x < 5; x++)
                g.BondX[g.At(x, 1)] = new float2(16000f, bend);

            int[] island = g.Solve(out int islands);
            Assert.AreEqual(expected, islands);
            if (expected == 0)
                return;

            // It broke at the root: the whole arm falls as one piece.
            for (int x = 1; x < 6; x++)
                Assert.AreEqual(island[g.At(1, 1)], island[g.At(x, 1)]);
            Assert.AreEqual(0, island[g.At(0, 1)]);
        }

        [Test]
        public void Hanging_FromAWeakJoint_Drops()
        {
            // A post, an arm on top, and a block hanging under the arm's end by a weak joint.
            using var g = new Graph(new int3(2, 3, 1));
            for (int y = 0; y < 3; y++)
                g.Mass[g.At(0, y)] = 100f;
            g.BondY[g.At(0, 0)] = Strong;
            g.BondY[g.At(0, 1)] = Strong;
            g.Support[0] = 1;
            g.Mass[g.At(1, 2)] = 100f;
            g.BondX[g.At(0, 2)] = Strong;
            g.Mass[g.At(1, 1)] = 100f;
            g.BondY[g.At(1, 1)] = new float2(16000f, 10f); // holds 25 kg hanging

            int[] island = g.Solve(out int islands);
            Assert.AreEqual(1, islands);
            Assert.AreNotEqual(0, island[g.At(1, 1)]);
            Assert.AreEqual(0, island[g.At(1, 2)]);
        }

        [Test]
        public void SeamCells_PartAtTheWeakJoint()
        {
            // Four blocks in a column; the joint between 1 and 2 is weak (glass), the rest brick.
            var blocks = new int3(1, 4, 1);
            var mass = new NativeArray<float>(4, Allocator.TempJob);
            var bx = new NativeArray<float2>(4, Allocator.TempJob);
            var by = new NativeArray<float2>(4, Allocator.TempJob);
            var bz = new NativeArray<float2>(4, Allocator.TempJob);
            var seeds = new NativeArray<float3>(2, Allocator.TempJob);
            var cell = new NativeArray<int>(4, Allocator.TempJob);
            for (int i = 0; i < 4; i++)
                mass[i] = 100f;
            by[0] = Strong;
            by[1] = new float2(300f, 1f);
            by[2] = Strong;
            // Seeds in blocks 0 and 2: block 1 is as near to both by count, but crossing the glass costs more.
            seeds[0] = new float3(1f, 1f, 1f);
            seeds[1] = new float3(1f, 9f, 1f);

            new SeamCellsJob { Blocks = blocks, Mass = mass, BondX = bx, BondY = by, BondZ = bz, Seeds = seeds, BlockCell = cell }.Run();
            int[] result = cell.ToArray();
            mass.Dispose();
            bx.Dispose();
            by.Dispose();
            bz.Dispose();
            seeds.Dispose();
            cell.Dispose();

            Assert.AreEqual(result[0], result[1], "block 1 goes with block 0, not across the glass");
            Assert.AreEqual(result[2], result[3]);
            Assert.AreNotEqual(result[1], result[2]);
        }
    }
}
