using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// What happens when the tank's health runs out: the driver is thrown out alive, the hull
    /// goes up in a blast that hurts nobody, and a burnt-out wreck stays where it stood.
    ///
    /// The character's DeathBehaviour is deliberately not used: it disposes the entity and drops
    /// it from the world, and the wreck is meant to stay — as an obstacle, not a vehicle.
    /// PlayerCharacterDeathController leaves vehicles alone for the same reason, so it does not clear
    /// the player's Character in the moment between the hit and the ejection.
    /// </summary>
    public sealed class TankDestroyBehaviour : IEntityInit, IEntityDispose
    {
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        /// <summary>How much of its colour the burnt hull keeps.</summary>
        private const float WRECK_SHADE = 0.22f;

        /// <summary>The blast goes off at the turret ring, not at the wheels.</summary>
        private const float BLAST_HEIGHT = 1.2f;

        /// <summary>Most spin (rad/s) a falling wreck is given.</summary>
        private const float DROP_SPIN = 2f;

        private readonly GameContext _gameContext;
        private readonly MonoEntity _vfxPrefab;
        private readonly CameraShakeArgs _shake;
        private readonly bool _fallOnDeath;

        private IEntity _entity;
        private Health _health;

        /// <param name="fallOnDeath">
        /// False (the tank): the wreck freezes where it stood. True (the helicopter): the wreck
        /// drops out of the air, tumbling, and stays where it lands.
        /// </param>
        public TankDestroyBehaviour(GameContext gameContext, MonoEntity vfxPrefab, CameraShakeArgs shake,
            bool fallOnDeath = false)
        {
            _gameContext = gameContext;
            _vfxPrefab = vfxPrefab;
            _shake = shake;
            _fallOnDeath = fallOnDeath;
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

            // First, while the vehicle still answers as one: the driver gets out and takes the
            // player's controls and the enemies' attention back.
            ControllableUseCase.Eject(_entity, player.GetCharacter(), _gameContext.GetEntityWorld());

            _entity.DelInteractableTag();
            _entity.DelDamageableTag();

            if (_entity.TryGetDeathTakenEvent(out IEvent<TakeDamageArgs> death))
                death.Invoke(new TakeDamageArgs(_entity));

            Vector3 blast = _entity.GetTransform().position + Vector3.up * BLAST_HEIGHT;
            SpawnBlast(blast);
            Shake(player);
            Burn();

            if (_fallOnDeath)
                Drop();
            else
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
        /// <summary>
        /// Nothing holds a dead helicopter up any more: gravity takes it, free to turn over, with a
        /// little spin of its own so it does not come down flat.
        /// </summary>
        private void Drop()
        {
            if (!_entity.TryGetRiggedBody(out Rigidbody body) || body == null)
                return;

            body.isKinematic = false;
            body.useGravity = true;
            body.constraints = RigidbodyConstraints.None;
            body.angularVelocity = Random.insideUnitSphere * DROP_SPIN;
        }

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
