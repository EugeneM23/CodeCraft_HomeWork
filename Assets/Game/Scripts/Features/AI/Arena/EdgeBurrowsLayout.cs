using System.Collections.Generic;
using UnityEngine;

namespace Game.AIKit.Adapters
{
    /// <summary>
    /// A stand-in arena for the sandbox, tests and the bench: <c>count</c> burrows spaced evenly along the edge of a
    /// rectangle, on the ground (the level grid's height when there is one, else the rectangle's floor), no craters,
    /// no cover spots. The game uses the Arenas team's service (<see cref="ArenaServiceBurrows"/>).
    /// </summary>
    public sealed class EdgeBurrowsLayout : IArenaBurrows
    {
        private static readonly Vector3[] NoCover = new Vector3[0];
        private readonly Vector3[] _burrows;
        private readonly bool[] _open;

        /// <param name="bounds">The arena (XZ rectangle; min.y is the floor).</param>
        /// <param name="count">Burrows along the edge.</param>
        /// <param name="ground">Height of the ground at a point (NaN: none known), e.g. the level grid's; null: the floor.</param>
        public EdgeBurrowsLayout(Bounds bounds, int count, System.Func<Vector3, float> ground = null)
        {
            Bounds = bounds;
            count = Mathf.Max(1, count);
            _burrows = new Vector3[count];
            _open = new bool[count];
            Vector3 min = bounds.min;
            Vector3 size = bounds.size;
            float perimeter = 2f * (size.x + size.z);
            for (int i = 0; i < count; i++)
            {
                float d = (i + 0.5f) / count * perimeter;
                Vector3 p;
                if (d < size.x)
                    p = new Vector3(min.x + d, min.y, min.z);
                else if ((d -= size.x) < size.z)
                    p = new Vector3(min.x + size.x, min.y, min.z + d);
                else if ((d -= size.z) < size.x)
                    p = new Vector3(min.x + size.x - d, min.y, min.z + size.z);
                else
                    p = new Vector3(min.x, min.y, min.z + size.z - (d - size.x));

                if (ground != null)
                {
                    float h = ground(p);
                    if (!float.IsNaN(h))
                        p.y = h;
                }

                _burrows[i] = p;
            }
        }

        /// <summary>A square of <paramref name="size"/> m round <paramref name="center"/> (on its ground) with <paramref name="count"/> burrows.</summary>
        public static EdgeBurrowsLayout Around(Vector3 center, float size, int count, System.Func<Vector3, float> ground = null)
        {
            var bounds = new Bounds();
            bounds.SetMinMax(new Vector3(center.x - size * 0.5f, center.y, center.z - size * 0.5f),
                new Vector3(center.x + size * 0.5f, center.y + 20f, center.z + size * 0.5f));
            return new EdgeBurrowsLayout(bounds, count, ground);
        }

        public Bounds Bounds { get; }

        public IReadOnlyList<Vector3> BurrowPoints => _burrows;

        public IReadOnlyList<Vector3> CoverPoints => NoCover;

        /// <summary>Burrows opened so far.</summary>
        public int OpenedCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _open.Length; i++)
                    if (_open[i])
                        n++;
                return n;
            }
        }

        public Vector3 OpenBurrow(int index)
        {
            _open[index] = true;
            return _burrows[index];
        }
    }
}
