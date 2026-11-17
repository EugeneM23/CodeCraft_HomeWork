using Atomic.Entities;
using Game.Voxels;
using UnityEngine;

namespace Game
{
    public class BulletCollisionBehaviour : IEntityInit, IEntityDispose
    {
        /// <summary>Radius (m) of the bite a bullet takes out of voxels: one or two 10 cm voxels.</summary>
        private const float VOXEL_CHIP_RADIUS = 0.13f;

        private CollisionEventReceiver _collisionEventReceiver;
        private IEntity _bullet;
        private GameFactory _gameFactory;

        public void Init(IEntity entity)
        {
            _gameFactory = GameContext.Instance.GetGameFactory();
            _collisionEventReceiver = entity.GetCollisionReceiver();
            _bullet = entity;
            _collisionEventReceiver.OnEntered += OnCollision;
        }

        public void Dispose(IEntity entity) => _collisionEventReceiver.OnEntered -= OnCollision;

        private void OnCollision(Collision collision)
        {
            // A bullet touching several colliders gets one OnCollisionEnter per collider in the same
            // physics step, even after the first one returned it to the pool. Only the first counts:
            // otherwise one bullet deals damage several times and is returned to the pool twice.
            if (!_bullet.GetGameObject().activeInHierarchy)
                return;

            collision.gameObject.TryGetComponent(out IEntity target);

            // Mud swallows a hit: nothing bounces off it, it only gets a dent.
            bool mud = MudImpactUseCase.IsMud(collision.collider);

            // Ricochet comes first, for explosive projectiles too: the tank shell bounces off walls
            // exactly as bullets do and goes off once it runs out of ricochets. It used to explode
            // before ricochet was even looked at.
            // Voxels take every hit: a shell digs into voxel ground or brick instead of skipping
            // off it, the way it went off on the ground the cursor pointed at.
            bool voxel = VoxelDamage.IsVoxel(collision.collider);

            if (!mud && !voxel && BulletHitUseCase.ShouldTryRicochet(_bullet, target) && TryRicochet(collision))
                return;

            // A blast marks the mud itself (ExplosionUseCase); a plain bullet leaves a dent here.
            if (mud && !ExplosionUseCase.IsExplosive(_bullet))
                MudImpactUseCase.Bullet(collision.collider, collision.GetContact(0).point,
                    _bullet.GetMoveDirection().Value);

            if (ExplosionUseCase.IsExplosive(_bullet))
                ExplosionUseCase.Explode(_bullet, collision.GetContact(0).point, collision.collider, _gameFactory);
            else if (target != null && target.HasDamageableTag())
            {
                DamageUseCase.TakeDamage(target, new TakeDamageArgs(_bullet));

                // A person shows the hit with blood; a tank's armour showed nothing at all.
                if (BulletHitUseCase.ShowsHitOnTarget(target))
                {
                    SpawnUseCase.SpawnCollisionHit(collision, _gameFactory, _bullet);
                    ChipVoxels(collision);
                }
            }
            else
            {
                DestructionUseCase.TryImpact(_bullet, collision);
                SpawnUseCase.SpawnCollisionHit(collision, _gameFactory, _bullet);
                ChipVoxels(collision);
            }

            SpawnUseCase.UnSpawnEntity(_gameFactory, _bullet);
        }

        /// <summary>A bullet bites a small hole out of a voxel wall, or out of a vehicle's voxel skin.</summary>
        private void ChipVoxels(Collision collision) =>
            VoxelDamage.Impact(collision.GetContact(0).point, _bullet.GetMoveDirection().Value, VOXEL_CHIP_RADIUS);

        /// <summary>True when the bullet flies on: it bounced off the surface, or already flies away from it.</summary>
        private bool TryRicochet(Collision collision)
        {
            Vector3 normal = collision.GetContact(0).normal;

            if (RicochetUseCase.IsMovingAway(_bullet.GetMoveDirection().Value, normal))
                return true;

            if (!RicochetUseCase.CanRicochet(_bullet, normal))
                return false;

            // Before the bounce: the impact ray follows the direction the bullet came from.
            DestructionUseCase.TryImpact(_bullet, collision, _bullet.GetRicochetImpactFactor());
            SpawnUseCase.SpawnCollisionHit(collision, _gameFactory, _bullet);
            RicochetUseCase.Ricochet(_bullet, normal);
            return true;
        }
    }
}
