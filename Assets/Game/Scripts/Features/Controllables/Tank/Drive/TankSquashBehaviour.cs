using System.Collections.Generic;
using Atomic.Entities;
using Game.Voxels;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Body parts the tank drives over burst from under its tracks: every physics step, while the tank moves, the
    /// pieces of voxel flesh lying in a low band under the hull (VoxelCorpses.Squash) are put away and each pops
    /// into a short splash of sticky goo thrown out to the side it was on (VoxelSpray.Goo). The flesh never
    /// touches the tank (VoxelGibs), so this is the only way the tracks meet it.
    /// </summary>
    public sealed class TankSquashBehaviour : IEntityInit, IEntityFixedTick
    {
        private static readonly Unity.Profiling.ProfilerMarker SquashMarker = new("Crush.Squash");

        /// <summary>Below this speed (m/s) nothing is run over.</summary>
        private const float MIN_SPEED = 0.4f;

        /// <summary>The band under the hull, from this far below its bottom to this far above it (m).</summary>
        private const float BELOW = 0.4f;
        private const float ABOVE = 0.9f;

        /// <summary>The band reaches this far (m) past the hull's front and back: what the tracks roll onto.</summary>
        private const float AHEAD = 0.3f;

        /// <summary>Drops of goo per m³ of a squashed piece, and the fewest and most a piece gives.</summary>
        private const float DROPS_PER_VOLUME = 60f;
        private const int MIN_DROPS = 14;
        private const int MAX_DROPS = 30;

        private const float GOO_SPEED = 6f;
        private const float GOO_SIZE = 0.1f;

        /// <summary>Volume (m³) of a part that splatters the tank's side at the most.</summary>
        private const float HEAVY_VOLUME = 0.25f;

        /// <summary>Share of the tank's velocity the goo carries.</summary>
        private const float INHERIT = 0.25f;

        private static readonly List<Bounds> Squashed = new(16);

        /// <summary>Blood, meat and a little green skin: what a burst body part is.</summary>
        private static readonly Color32[] Goo =
        {
            new Color(0.62f, 0.04f, 0.03f), new Color(0.45f, 0.03f, 0.02f), new Color(0.62f, 0.04f, 0.03f),
            new Color(0.55f, 0.12f, 0.12f), new Color(0.70f, 0.28f, 0.28f), new Color(0.36f, 0.55f, 0.24f)
        };

        private readonly Rigidbody _body;
        private BoxCollider _hull;

        public TankSquashBehaviour(Rigidbody body) => _body = body;

        public void Init(IEntity entity) => _hull = BiggestBox(_body);

        public void FixedTick(IEntity entity, float deltaTime)
        {
            if (_hull == null || !_hull.enabled || VoxelCorpses.CorpseCount == 0)
                return;
            Vector3 velocity = _body.linearVelocity;
            if (velocity.sqrMagnitude < MIN_SPEED * MIN_SPEED)
                return;

            using (SquashMarker.Auto())
            {
                Transform frame = _hull.transform;
                Vector3 half = Vector3.Scale(_hull.size, frame.lossyScale) * 0.5f;
                Vector3 centre = frame.TransformPoint(_hull.center);
                Vector3 up = frame.up;
                Vector3 bottom = centre - up * half.y;
                var band = new Vector3(half.x, (BELOW + ABOVE) * 0.5f, half.z + AHEAD);
                Vector3 bandCentre = bottom + up * ((ABOVE - BELOW) * 0.5f);

                if (VoxelCorpses.Squash(bandCentre, frame.rotation, band, Squashed) == 0)
                    return;

                for (int i = 0; i < Squashed.Count; i++)
                {
                    Bounds piece = Squashed[i];
                    Vector3 side = frame.right * Mathf.Sign(Vector3.Dot(piece.center - centre, frame.right));
                    Vector3 size = piece.size;
                    int drops = Mathf.Clamp(Mathf.RoundToInt(size.x * size.y * size.z * DROPS_PER_VOLUME), MIN_DROPS, MAX_DROPS);
                    VoxelSpray.Goo(piece.center, Vector3.up + side * 0.7f, drops, GOO_SPEED, 0.45f, Goo, GOO_SIZE,
                        velocity * INHERIT, Mathf.Min(0.3f, piece.extents.magnitude * 0.5f));
                    // The track, road wheels and lower hull round it take the blood; a big part splatters up the side.
                    TankBloodStains.Squash(new Vector3(piece.center.x, piece.min.y, piece.center.z), side,
                        size.x * size.y * size.z / HEAVY_VOLUME);
                }
            }
        }

        private static BoxCollider BiggestBox(Rigidbody body)
        {
            BoxCollider biggest = null;
            float volume = 0f;
            if (body == null)
                return null;
            foreach (BoxCollider box in body.GetComponents<BoxCollider>())
            {
                if (box.isTrigger)
                    continue;
                Vector3 size = Vector3.Scale(box.size, box.transform.lossyScale);
                float v = Mathf.Abs(size.x * size.y * size.z);
                if (v > volume)
                {
                    volume = v;
                    biggest = box;
                }
            }

            return biggest;
        }
    }
}
