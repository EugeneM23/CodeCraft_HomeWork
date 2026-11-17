using System.Collections.Generic;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Lets its owner — the tank, an enemy — pass through everything knocked out of the voxels:
    /// flying debris and whatever has come down on the floor, simulated or frozen (a creature's
    /// voxel flesh that settled keeps a kinematic body). Nobody is bumped, lifted or stopped by
    /// them, and they are not shoved about.
    ///
    /// Each physics step, before the simulation, it looks for loose pieces around the owner and
    /// switches collisions off between them and the owner's colliders, pair by pair. Bullets,
    /// blasts, the floor and other pieces still collide with them as before.
    ///
    /// It looks on Debris and Rubble, where loose pieces are: everything there is loose, whatever
    /// its body.
    ///
    /// Pairs are checked every step rather than remembered: new pieces keep coming down round the
    /// owner, and a pair already switched off costs one lookup.
    /// </summary>
    public sealed class PassThroughLoosePiecesBehaviour : IEntityInit, IEntityFixedTick
    {
        /// <summary>
        /// How far (m) around the owner a piece is caught before it arrives. One 50 Hz step at
        /// 75 m/s; a faster piece may touch the owner once before it is seen.
        /// </summary>
        public const float DEFAULT_MARGIN = 1.5f;

        // Where loose pieces are.
        private const int LOOSE_LAYERS = (1 << PhysicsLayers.Debris) | (1 << PhysicsLayers.Rubble);

        // Right after a blast the box can take in hundreds of loose pieces. Every Regax that died
        // in voxels leaves 150–200 boxes of flesh lying for the rest of the game. A query that fills
        // the buffer doubles it and runs again: a piece left out of the buffer would block the owner.
        private static Collider[] s_hits = new Collider[1024];

        /// <summary>Largest the buffer grows; past it the farthest pieces in the query may still block.</summary>
        private const int MAX_HITS = 1 << 16;

        private readonly float _margin;
        private readonly List<Collider> _own = new();

        public PassThroughLoosePiecesBehaviour(float margin = DEFAULT_MARGIN)
        {
            _margin = margin;
        }

        public void Init(IEntity entity)
        {
            _own.Clear();
            entity.GetGameObject().GetComponentsInChildren(_own);
            _own.RemoveAll(collider => collider.isTrigger);
        }

        public void FixedTick(IEntity entity, float deltaTime)
        {
            if (!TryGetArea(out Bounds area))
                return;

            int count = OverlapLooseLayers(area);

            // A kinematic body there is a piece that came to rest.
            for (int i = 0; i < count; i++)
                PassThrough(s_hits[i]);
        }

        /// <summary>Every collider on Debris and Rubble in the area; the buffer grows until they all fit.</summary>
        private static int OverlapLooseLayers(Bounds area)
        {
            while (true)
            {
                int count = Physics.OverlapBoxNonAlloc(area.center, area.extents, s_hits, Quaternion.identity,
                    LOOSE_LAYERS, QueryTriggerInteraction.Ignore);
                if (count < s_hits.Length || s_hits.Length >= MAX_HITS)
                    return count;

                s_hits = new Collider[s_hits.Length * 2];
            }
        }

        private void PassThrough(Collider piece)
        {
            for (int c = 0; c < _own.Count; c++)
            {
                Collider own = _own[c];
                if (IsActive(own) && !Physics.GetIgnoreCollision(own, piece))
                    Physics.IgnoreCollision(own, piece, true);
            }
        }

        // The owner's colliders together, with the margin around them.
        private bool TryGetArea(out Bounds area)
        {
            area = default;
            bool found = false;

            for (int c = 0; c < _own.Count; c++)
            {
                Collider own = _own[c];
                if (!IsActive(own))
                    continue;

                if (found)
                {
                    area.Encapsulate(own.bounds);
                }
                else
                {
                    area = own.bounds;
                    found = true;
                }
            }

            if (found)
                area.Expand(_margin * 2f);

            return found;
        }

        private static bool IsActive(Collider collider) =>
            collider != null && collider.enabled && collider.gameObject.activeInHierarchy;
    }
}
