using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// How much a seam between two pieces of a wall takes before it gives, in pascals of its
    /// contact area, for each way it can be loaded.
    /// </summary>
    public readonly struct SeamStrength
    {
        /// <summary>The piece above rests on the seam.</summary>
        public readonly float compression;

        /// <summary>The seam holds a piece from the side.</summary>
        public readonly float shear;

        /// <summary>A piece hangs from the seam.</summary>
        public readonly float tension;

        /// <summary>A piece resting on a slope gentler than atan(friction) is held by friction alone.</summary>
        public readonly float friction;

        public SeamStrength(float compression, float shear, float tension, float friction)
        {
            this.compression = compression;
            this.shear = shear;
            this.tension = tension;
            this.friction = friction;
        }
    }

    /// <summary>
    /// Finds the part of a damaged wall that its seams can no longer hold up.
    ///
    /// The wall is a graph: pieces joined by seams, some pieces anchored. Every other piece's
    /// weight has to reach an anchor through the seams, and a seam passes on only so much of it,
    /// depending on how big the contact is and which way it faces: a piece resting on the one below
    /// presses the seam, one held from the side shears it, one hanging from above pulls it apart.
    /// Weight flowing through seams of limited capacity is a max-flow problem. When the wall cannot
    /// carry all of its weight, the minimum cut is the line along which it gives way, and
    /// everything on the far side of it falls. Of all such lines it is the one that drops the least.
    ///
    /// Only the vertical balance is modelled. A long ledge breaks because its root seams cannot pass
    /// on its weight, not because of a bending moment, so a ledge holds out to about
    /// shear / (density × g) whatever its height.
    /// </summary>
    public sealed class WallLoadSolver
    {
        // Newtons. Far below the weight of any piece, far above rounding errors of the flow.
        private const double EPSILON = 1e-6;

        private readonly List<Vector3> _centres = new();
        private readonly List<float> _masses = new();
        private readonly List<bool> _anchored = new();

        private readonly List<int> _seamA = new();
        private readonly List<int> _seamB = new();
        private readonly List<float> _seamArea = new();

        // Residual graph of the flow. Arc e and its reverse e ^ 1 are added together.
        private int[] _head = Array.Empty<int>();
        private int[] _level = Array.Empty<int>();
        private int[] _cursor = Array.Empty<int>();
        private readonly List<int> _arcTo = new();
        private readonly List<int> _arcNext = new();
        private readonly List<double> _arcCapacity = new();
        private readonly Queue<int> _queue = new();

        public int PieceCount => _centres.Count;
        public int SeamCount => _seamA.Count;

        public void Clear()
        {
            _centres.Clear();
            _masses.Clear();
            _anchored.Clear();
            _seamA.Clear();
            _seamB.Clear();
            _seamArea.Clear();
        }

        /// <returns>The index of the piece.</returns>
        public int AddPiece(Vector3 centre, float mass, bool anchored)
        {
            _centres.Add(centre);
            _masses.Add(mass);
            _anchored.Add(anchored);
            return _centres.Count - 1;
        }

        /// <param name="area">Contact area of the two pieces, m².</param>
        /// <returns>The index of the seam.</returns>
        public int AddSeam(int a, int b, float area)
        {
            _seamA.Add(a);
            _seamB.Add(b);
            _seamArea.Add(area);
            return _seamA.Count - 1;
        }

        public void GetSeam(int seam, out int a, out int b)
        {
            a = _seamA[seam];
            b = _seamB[seam];
        }

        /// <summary>
        /// Fills <paramref name="brokenSeams"/> with the seams the wall gives way along, or leaves it
        /// empty when the wall holds. Cutting them separates every piece that falls from every piece
        /// that stays.
        /// </summary>
        public void FindFailure(SeamStrength strength, Vector3 gravity, List<int> brokenSeams)
        {
            brokenSeams.Clear();

            int pieces = _centres.Count;
            if (pieces == 0)
                return;

            int source = pieces;
            int sink = pieces + 1;

            BuildFlowGraph(strength, gravity, source, sink);
            MaxFlow(source, sink);

            // Whatever the source still reaches carries weight the wall cannot pass on to an anchor.
            MarkReachable(source);

            for (int s = 0; s < _seamA.Count; s++)
                if ((_level[_seamA[s]] >= 0) != (_level[_seamB[s]] >= 0))
                    brokenSeams.Add(s);
        }

        /// <summary>
        /// The most weight (N) a seam passes on from one piece to the other, the other lying in
        /// <paramref name="direction"/> from the first. The weight acts straight down, while the seam
        /// is taken to face along the line between the pieces' centres; the part of the weight
        /// across the seam presses or pulls it, the part along it shears it.
        /// </summary>
        public static double Capacity(SeamStrength strength, Vector3 direction, Vector3 down, float area)
        {
            // 1 when the other piece lies right below, 0 beside, -1 right above.
            float across = Vector3.Dot(direction, down);
            float along = Mathf.Sqrt(Mathf.Max(0f, 1f - across * across));
            double capacity = double.PositiveInfinity;

            if (across > 0f)
            {
                capacity = strength.compression * area / across;

                // Pressing the seam lends it friction against sliding.
                float slide = along - strength.friction * across;
                if (slide > 0f)
                    capacity = Math.Min(capacity, strength.shear * area / slide);
            }
            else
            {
                if (across < 0f)
                    capacity = strength.tension * area / -across;

                if (along > 0f)
                    capacity = Math.Min(capacity, strength.shear * area / along);
            }

            return capacity;
        }

        private void BuildFlowGraph(SeamStrength strength, Vector3 gravity, int source, int sink)
        {
            int nodes = _centres.Count + 2;
            if (_head.Length < nodes)
            {
                _head = new int[nodes];
                _level = new int[nodes];
                _cursor = new int[nodes];
            }

            for (int i = 0; i < nodes; i++)
                _head[i] = -1;

            _arcTo.Clear();
            _arcNext.Clear();
            _arcCapacity.Clear();

            Vector3 down = gravity.normalized;
            float g = gravity.magnitude;

            // Anchors take any load; every other piece brings its weight in from the source.
            for (int p = 0; p < _centres.Count; p++)
            {
                if (_anchored[p])
                    AddArc(p, sink, double.PositiveInfinity, 0d);
                else
                    AddArc(source, p, _masses[p] * g, 0d);
            }

            for (int s = 0; s < _seamA.Count; s++)
            {
                int a = _seamA[s];
                int b = _seamB[s];
                Vector3 direction = (_centres[b] - _centres[a]).normalized;
                float area = _seamArea[s];

                AddArc(a, b,
                    Capacity(strength, direction, down, area),
                    Capacity(strength, -direction, down, area));
            }
        }

        private void AddArc(int from, int to, double capacity, double reverseCapacity)
        {
            _arcTo.Add(to);
            _arcNext.Add(_head[from]);
            _arcCapacity.Add(capacity);
            _head[from] = _arcTo.Count - 1;

            _arcTo.Add(from);
            _arcNext.Add(_head[to]);
            _arcCapacity.Add(reverseCapacity);
            _head[to] = _arcTo.Count - 1;
        }

        // Dinic: push blocking flows along shortest paths until the sink is out of reach.
        private void MaxFlow(int source, int sink)
        {
            int nodes = _centres.Count + 2;

            while (true)
            {
                MarkReachable(source);
                if (_level[sink] < 0)
                    return;

                Array.Copy(_head, _cursor, nodes);
                while (Push(source, sink, double.PositiveInfinity) > EPSILON)
                {
                }
            }
        }

        // Breadth-first levels over the arcs with capacity left; -1 where the source cannot reach.
        private void MarkReachable(int source)
        {
            int nodes = _centres.Count + 2;
            for (int i = 0; i < nodes; i++)
                _level[i] = -1;

            _level[source] = 0;
            _queue.Clear();
            _queue.Enqueue(source);

            while (_queue.Count > 0)
            {
                int node = _queue.Dequeue();
                for (int arc = _head[node]; arc >= 0; arc = _arcNext[arc])
                {
                    int next = _arcTo[arc];
                    if (_level[next] >= 0 || _arcCapacity[arc] <= EPSILON)
                        continue;

                    _level[next] = _level[node] + 1;
                    _queue.Enqueue(next);
                }
            }
        }

        private double Push(int node, int sink, double amount)
        {
            if (node == sink)
                return amount;

            for (; _cursor[node] >= 0; _cursor[node] = _arcNext[_cursor[node]])
            {
                int arc = _cursor[node];
                int next = _arcTo[arc];
                if (_arcCapacity[arc] <= EPSILON || _level[next] != _level[node] + 1)
                    continue;

                double pushed = Push(next, sink, Math.Min(amount, _arcCapacity[arc]));
                if (pushed <= EPSILON)
                    continue;

                _arcCapacity[arc] -= pushed;
                _arcCapacity[arc ^ 1] += pushed;
                return pushed;
            }

            return 0d;
        }
    }
}
