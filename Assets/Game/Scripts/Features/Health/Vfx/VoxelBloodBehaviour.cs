using Atomic.Elements;
using Atomic.Entities;
using Game.Voxels;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// A living body bleeds voxel drops where it is hit: out of the point of its collider nearest
    /// the bullet or the blast, along the surface's outward normal; a claw's hit bleeds from the
    /// chest. The drops land as tiles and lie all game. Takes the place of TakeDamageBloodBehaviour
    /// (particle blood) on Regax.
    /// </summary>
    public sealed class VoxelBloodBehaviour : IEntityInit, IEntityDispose
    {
        private readonly Collider _body;
        private readonly Transform _chest;
        private readonly VoxelBloodConfig _config;
        private Color32[] _colors;
        private IEvent<TakeDamageArgs> _damageTakenEvent;

        public VoxelBloodBehaviour(Collider body, Transform chest, VoxelBloodConfig config)
        {
            _body = body;
            _chest = chest;
            _config = config;
        }

        public void Init(IEntity entity)
        {
            _colors = _config.blood.ToVoxels();
            _damageTakenEvent = entity.GetDamageTakenEvent();
            _damageTakenEvent.Subscribe(this.OnDamageTaken);
        }

        // Null when disposed without having been initialised.
        public void Dispose(IEntity entity) => _damageTakenEvent?.Unsubscribe(this.OnDamageTaken);

        private void OnDamageTaken(TakeDamageArgs args)
        {
            IEntity source = args?.source;
            if (source == null || _body == null || !GibRules.TryGetPosition(source, out Vector3 from))
                return;

            int count;
            Vector3 point;
            if (ExplosionUseCase.IsExplosive(source))
            {
                count = _config.blastDrops;
                point = _body.ClosestPoint(from);
            }
            else if (source.HasMeleeWeaponTag() && _chest != null)
            {
                count = Random.Range(_config.meleeDrops.x, _config.meleeDrops.y + 1);
                point = _chest.position;
            }
            else
            {
                count = Random.Range(_config.bulletDrops.x, _config.bulletDrops.y + 1);
                point = _body.ClosestPoint(from);
            }

            Vector3 outward = point - _body.bounds.center;
            if (outward.sqrMagnitude < 1e-4f)
                outward = point - from;

            VoxelSpray.Burst(point, outward, count, _config.minSpeed, _config.maxSpeed, _config.spread, _colors,
                _config.blood.dropSize);
        }
    }
}
