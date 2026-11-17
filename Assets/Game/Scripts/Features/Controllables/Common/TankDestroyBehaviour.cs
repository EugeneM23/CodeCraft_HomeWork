using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// What happens when the tank's health runs out: the hull goes up in a blast that hurts
    /// nobody, and a burnt-out wreck stays where it stood. It stays the player's Character: what
    /// follows a lost tank (end of the fight) is up to the game flow.
    ///
    /// The character's DeathBehaviour is deliberately not used: it disposes the entity and drops
    /// it from the world, and the wreck is meant to stay — as an obstacle, not a vehicle.
    /// </summary>
    public sealed class TankDestroyBehaviour : IEntityInit, IEntityDispose
    {
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        /// <summary>How much of its colour the burnt hull keeps.</summary>
        private const float WRECK_SHADE = 0.22f;

        /// <summary>The blast goes off at the turret ring, not at the wheels.</summary>
        private const float BLAST_HEIGHT = 1.2f;

        private readonly GameContext _gameContext;
        private readonly MonoEntity _vfxPrefab;
        private readonly CameraShakeArgs _shake;

        private IEntity _entity;
        private Health _health;

        public TankDestroyBehaviour(GameContext gameContext, MonoEntity vfxPrefab, CameraShakeArgs shake)
        {
            _gameContext = gameContext;
            _vfxPrefab = vfxPrefab;
            _shake = shake;
        }

        public void Init(IEntity entity)
        {
            _entity = entity;
            _health = entity.GetHealth();
            _health.OnHealthEmpty += OnDestroyed;
        }

        public void Dispose(IEntity entity)
        {
            if (_health != null)
                _health.OnHealthEmpty -= OnDestroyed;

            _health = null;
        }

        private void OnDestroyed()
        {
            IPlayerContext player = _gameContext.GetPlayerContext();

            _entity.DelDamageableTag();

            if (_entity.TryGetDeathTakenEvent(out IEvent<TakeDamageArgs> death))
                death.Invoke(new TakeDamageArgs(_entity));

            Vector3 blast = _entity.GetTransform().position + Vector3.up * BLAST_HEIGHT;
            SpawnBlast(blast);
            Shake(player);
            Burn();

            Park();

            _entity.Disable();
        }

        private void SpawnBlast(Vector3 point)
        {
            if (_vfxPrefab == null)
                return;

            IEntity vfx = _gameContext.GetGameFactory().Create(_vfxPrefab);
            vfx.GetTransform().SetPositionAndRotation(point, Quaternion.identity);
        }

        private void Shake(IPlayerContext player)
        {
            if (_shake == null || _shake.ShakeDuration <= 0f)
                return;

            CameraShakeUseCase.ShakePlayerCamera(player, _shake);
        }

        /// <summary>
        /// Darkens every visible part through a property block, so the shared materials — and every
        /// other tank built from them — keep their colour.
        /// </summary>
        private void Burn()
        {
            if (!_entity.TryGetHullVisual(out Transform hull) || hull == null)
                return;

            MaterialPropertyBlock block = new();

            foreach (Renderer renderer in hull.GetComponentsInChildren<Renderer>())
            {
                Material material = renderer.sharedMaterial;
                if (material == null || !material.HasProperty(ColorId))
                    continue;

                renderer.GetPropertyBlock(block);
                Color colour = material.GetColor(ColorId) * WRECK_SHADE;
                colour.a = 1f;
                block.SetColor(ColorId, colour);
                renderer.SetPropertyBlock(block);
            }
        }

        /// <summary>
        /// The suspension and the drive stop with the entity: left dynamic, the hull would drop onto
        /// its frictionless belly and slide on for ever. Kinematic, it stays where it died, still
        /// on its wheels, and still blocks.
        /// </summary>
        private void Park()
        {
            if (!_entity.TryGetRiggedBody(out Rigidbody body) || body == null)
                return;

            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
        }
    }
}
