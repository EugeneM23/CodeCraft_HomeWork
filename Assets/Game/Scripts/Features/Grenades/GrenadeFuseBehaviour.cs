using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Burns the grenade's fuse and sets the grenade off where it lies once the fuse runs out:
    /// an explosion (ExplosionInstaller) or a flash (FlashInstaller), whichever the grenade carries.
    /// The grenade does not go off on contact: it bounces and rolls until then.
    /// </summary>
    /// <remarks>
    /// A fresh fuse is already expired (CooldownTimer starts at zero). GrenadeThrowAction resets it
    /// right after renting the grenade from the pool, before this behaviour ever ticks it.
    /// </remarks>
    public sealed class GrenadeFuseBehaviour : IEntityInit, IEntityTick
    {
        private CooldownTimer _fuse;
        private Transform _transform;
        private GameFactory _factory;

        public void Init(IEntity entity)
        {
            _fuse = entity.GetGrenadeFuse();
            _transform = entity.GetTransform();
            _factory = GameContext.Instance.GetGameFactory();
        }

        public void Tick(IEntity entity, float deltaTime)
        {
            _fuse.Tick(deltaTime);

            if (!_fuse.IsExpired())
                return;

            Vector3 point = _transform.position;

            // No collider: the grenade lies next to whatever it lands by rather than striking it,
            // so the explosion skips the crumbs it would otherwise tint with the struck material.
            if (ExplosionUseCase.IsExplosive(entity))
                ExplosionUseCase.Explode(entity, point, null, _factory);

            if (FlashUseCase.IsFlash(entity))
                FlashUseCase.Flash(entity, point, _factory);

            SpawnUseCase.UnSpawnEntity(_factory, entity);
        }
    }
}
