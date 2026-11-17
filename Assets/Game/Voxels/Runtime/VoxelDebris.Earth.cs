using Unity.Mathematics;
using UnityEngine;

namespace Game.Voxels
{
    /// <summary>What to throw with <see cref="VoxelDebris.ThrowEarth"/>.</summary>
    public struct EarthThrow
    {
        /// <summary>Where the earth leaves the ground, world space.</summary>
        public Vector3 Point;
        /// <summary>Mean velocity of the throw, m/s.</summary>
        public Vector3 Velocity;
        /// <summary>m³ of earth thrown; about 0.008 m³ per 20 cm cube.</summary>
        public float Volume;
        /// <summary>0 - one tight clump, 1 - a wide fan of small clumps and crumbs.</summary>
        public float Spread;
        /// <summary>Turf / surface colour (top cubes).</summary>
        public Color32 Top;
        /// <summary>Earth under it (dry or wet mud).</summary>
        public Color32 Body;
    }

    public sealed partial class VoxelDebris
    {
        private const float EARTH_CUBE = 0.2f;
        private const float EARTH_CUBE_VOLUME = EARTH_CUBE * EARTH_CUBE * EARTH_CUBE;
        /// <summary>Radius (in cubes) of the ball a clump is cut from.</summary>
        private const int BALL_RADIUS = 4;
        /// <summary>A throw of more earth than this knocks out dust.</summary>
        private const float DUST_VOLUME = 0.05f;
        private const int MAX_CLUMPS = 4;

        /// <summary>Grid cells of a ball of BALL_RADIUS, nearest to its centre first; built once.</summary>
        private static readonly int3[] s_ballOffsets = BuildBallOffsets();

        private static int3[] BuildBallOffsets()
        {
            var list = new System.Collections.Generic.List<int3>();
            for (int x = -BALL_RADIUS; x <= BALL_RADIUS; x++)
            for (int y = -BALL_RADIUS; y <= BALL_RADIUS; y++)
            for (int z = -BALL_RADIUS; z <= BALL_RADIUS; z++)
            {
                if (x * x + y * y + z * z <= BALL_RADIUS * BALL_RADIUS)
                    list.Add(new int3(x, y, z));
            }

            list.Sort((a, b) =>
            {
                int d = math.dot(a, a).CompareTo(math.dot(b, b));
                if (d != 0) return d;
                d = a.y.CompareTo(b.y);
                if (d != 0) return d;
                d = a.x.CompareTo(b.x);
                return d != 0 ? d : a.z.CompareTo(b.z);
            });
            return list.ToArray();
        }

        /// <summary>Throws earth as a few clumps, crumbs and a puff of dust. Returns cubes thrown; 0 when full.</summary>
        public int ThrowEarth(in EarthThrow earth)
        {
            Complete();
            int total = math.max(1, (int)math.round(earth.Volume / EARTH_CUBE_VOLUME));
            int crumbs = total < MIN_PIECE ? 0 : (int)math.round(total * CRUMB_SHARE);
            int inClumps = total - crumbs;
            float spread = Mathf.Clamp01(earth.Spread);
            int k = math.clamp(1 + (int)math.round(spread * (MAX_CLUMPS - 1)), 1, MAX_CLUMPS);
            k = math.max(1, math.min(k, inClumps / MIN_PIECE));
            // A clump is cut from one ball; the rest of a very big throw would not fit it, so it goes as crumbs.
            int perClump = inClumps / k;
            int maxClump = s_ballOffsets.Length;
            if (perClump + 1 > maxClump)
            {
                crumbs += inClumps - k * maxClump;
                inClumps = k * maxClump;
            }

            int dust = earth.Volume > DUST_VOLUME ? MIN_DUST + (int)math.round(spread * MIN_DUST) : 0;
            if (!HasRoom(total + dust) || _clusters.Length + k + crumbs + dust > _capacity)
                return 0;

            float speed = earth.Velocity.magnitude;
            int left = inClumps;
            for (int j = 0; j < k; j++)
            {
                int m = left / (k - j);
                left -= m;
                Vector2 jitter = UnityEngine.Random.insideUnitCircle * (spread * 0.5f);
                Vector3 center = earth.Point + new Vector3(jitter.x, 0f, jitter.y);
                Vector3 velocity = earth.Velocity + UnityEngine.Random.insideUnitSphere * (speed * 0.3f * spread);
                int cluster = BeginCluster(center, velocity, UnityEngine.Random.Range(2f, 6f));
                if (cluster < 0)
                    break;

                int top = int.MinValue;
                for (int i = 0; i < m; i++)
                    top = math.max(top, s_ballOffsets[i].y);
                for (int i = 0; i < m; i++)
                {
                    int3 o = s_ballOffsets[i];
                    Vector3 at = center + new Vector3(o.x, o.y, o.z) * EARTH_CUBE;
                    AddCube(cluster, at, EARTH_CUBE, o.y == top ? earth.Top : earth.Body);
                }
            }

            for (int i = 0; i < crumbs; i++)
            {
                Vector3 jitter = UnityEngine.Random.insideUnitSphere * (0.1f + spread * 0.3f);
                Vector3 velocity = earth.Velocity * UnityEngine.Random.Range(0.6f, 1.1f) +
                                   UnityEngine.Random.insideUnitSphere * (2f * (0.3f + spread));
                Spawn(earth.Point + jitter, velocity, earth.Body, EARTH_CUBE * UnityEngine.Random.Range(0.75f, 0.95f));
            }

            AddDust(earth.Point, earth.Body, dust);
            return total + dust;
        }

        /// <summary>
        /// A puff of dust on the main thread: small cubes of <paramref name="color"/>, a shade lighter,
        /// flying up and out, each its own cluster flagged DUST; the same numbers as the break's dust.
        /// </summary>
        private void AddDust(Vector3 at, Color32 color, int count)
        {
            Color c = color;
            var lighter = new float3(c.r, c.g, c.b);
            lighter = math.lerp(lighter, new float3(1f), DUST_LIGHTEN);
            for (int j = 0; j < count; j++)
            {
                if (_clusters.Length >= _capacity || _cubes.Length >= _capacity)
                    return;

                Vector3 direction = UnityEngine.Random.onUnitSphere;
                direction.y = Mathf.Abs(direction.y);
                float size = UnityEngine.Random.Range(DUST_SIZE_MIN, DUST_SIZE_MAX);
                _clusters.Add(new Cluster
                {
                    Position = at,
                    Velocity = direction * UnityEngine.Random.Range(DUST_SPEED_MIN, DUST_SPEED_MAX),
                    Rotation = UnityEngine.Random.rotation,
                    Spin = UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(DUST_SPIN_MIN, DUST_SPIN_MAX),
                    Life = UnityEngine.Random.Range(DUST_LIFE_MIN, DUST_LIFE_MAX),
                    Count = 1,
                    Radius = size * 0.5f,
                    Flags = DUST
                });
                _cubes.Add(new Cube
                {
                    Cluster = _clusters.Length - 1,
                    Offset = float3.zero,
                    Size = size,
                    Color = new float4(lighter, c.a)
                });
            }
        }
    }
}
