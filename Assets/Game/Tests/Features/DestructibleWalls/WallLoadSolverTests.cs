using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>
    /// Walls of concrete blocks a quarter of a metre across, the bottom row anchored, laid out on a
    /// grid so that the expected line of failure can be worked out by hand.
    /// </summary>
    public sealed class WallLoadSolverTests
    {
        private const float CELL = 0.25f;
        private const float DENSITY = 2400f;
        private const float MASS = CELL * CELL * CELL * DENSITY; // 37.5 kg, 368 N
        private const float AREA = CELL * CELL;

        private static readonly Vector3 Gravity = new(0f, -9.81f, 0f);

        // A seam holds 1250 N from the side and 625 N hanging: a ledge holds out to about
        // shear / (density × g) = 0.85 m, a block hangs on one seam but two blocks do not.
        private static readonly SeamStrength Concrete = new(compression: 1500e3f, shear: 20e3f, tension: 10e3f, friction: 0.6f);

        private WallLoadSolver _solver;
        private int[,] _piece;
        private readonly List<int> _broken = new();

        [Test]
        public void IntactWall_NothingGivesWay()
        {
            BuildWall(16, 12, (x, y) => false);

            Assert.That(BrokenSeams(), Is.Empty);
        }

        [Test]
        public void LongLedgeOnANarrowColumn_TearsOffWhereItMeetsTheColumn()
        {
            // The hole runs out of the left edge: the top 1.25 m hang 3.5 m out from a 0.5 m column.
            BuildWall(16, 12, (x, y) => x < 14 && y >= 2 && y <= 6);

            var expected = new List<(int, int)>();
            for (int y = 7; y < 12; y++)
                expected.Add(Pair(_piece[13, y], _piece[14, y]));

            Assert.That(BrokenSeams(), Is.EquivalentTo(expected));
        }

        [Test]
        public void ShortLedge_Holds()
        {
            // 0.75 m out: 5.5 kN on root seams that take 6.25 kN.
            BuildWall(16, 12, (x, y) => x < 3 && y >= 2 && y <= 6);

            Assert.That(BrokenSeams(), Is.Empty);
        }

        [Test]
        public void StripHangingFromALintel_DropsWhole()
        {
            // A 1 m window; four blocks still hang from the middle of its lintel.
            BuildWall(16, 12, (x, y) => x >= 6 && x <= 9 && y >= 2 && y <= 6 && !(x == 7 && y >= 3));

            Assert.That(BrokenSeams(), Is.EquivalentTo(new[] { Pair(_piece[7, 6], _piece[7, 7]) }));
        }

        [Test]
        public void SingleBlockHangingFromALintel_Holds()
        {
            BuildWall(16, 12, (x, y) => x >= 6 && x <= 9 && y >= 2 && y <= 6 && !(x == 7 && y == 6));

            Assert.That(BrokenSeams(), Is.Empty);
        }

        // Builds a wall `columns` wide and `rows` tall, its bottom row anchored, with holes where
        // `hole` says so.
        private void BuildWall(int columns, int rows, Func<int, int, bool> hole)
        {
            _solver = new WallLoadSolver();
            _piece = new int[columns, rows];

            for (int x = 0; x < columns; x++)
            for (int y = 0; y < rows; y++)
                _piece[x, y] = hole(x, y) ? -1 : _solver.AddPiece(new Vector3(x * CELL, y * CELL, 0f), MASS, y == 0);

            for (int x = 0; x < columns; x++)
            for (int y = 0; y < rows; y++)
            {
                if (_piece[x, y] < 0)
                    continue;

                if (x + 1 < columns && _piece[x + 1, y] >= 0)
                    _solver.AddSeam(_piece[x, y], _piece[x + 1, y], AREA);

                if (y + 1 < rows && _piece[x, y + 1] >= 0)
                    _solver.AddSeam(_piece[x, y], _piece[x, y + 1], AREA);
            }
        }

        private List<(int, int)> BrokenSeams()
        {
            _solver.FindFailure(Concrete, Gravity, _broken);

            var pairs = new List<(int, int)>();
            foreach (int seam in _broken)
            {
                _solver.GetSeam(seam, out int a, out int b);
                pairs.Add(Pair(a, b));
            }

            return pairs;
        }

        private static (int, int) Pair(int a, int b) => a < b ? (a, b) : (b, a);
    }
}
