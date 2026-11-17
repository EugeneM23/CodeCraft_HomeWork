using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public sealed class PlayerCharacterCoreInstaller : MonoEntityInstaller
    {
        [Header("Movement Settings")] [SerializeField]
        private float _moveSpeed = 15f;

        [SerializeField] private float _rotationSpeed = 15f;

        [Header("Health Settings")] [SerializeField]
        private int _health = 100;

        [Header("Combat Settings")] [SerializeField]
        private MonoEntity _weapon;

        [SerializeField] private Transform _weaponRoot;

        [Header("Core Components")] [SerializeField]
        private Transform _characterRoot;
        [SerializeField] private Rigidbody _rigidbody;
        [SerializeField] private Transform _cameraPoint;

        [Header("Interaction")] [SerializeField]
        private InteractInstaller _interactInstaller;

        private GameContext _gameContext;

        public override void Install(IEntity entity)
        {
            _gameContext = GameContext.Instance;
            
            // Core
            entity.AddDamageableTag();
            entity.AddPlayerTag();
            entity.AddGameObject(transform.gameObject);
            entity.AddTransform(_characterRoot);
            entity.AddRiggedBody(_rigidbody);

            // Health
            HealthUseCase.AddHealthWithEvents(entity, _health);
            entity.AddBehaviour(new DeathBehaviour(_gameContext));

            // Movement
            WalkingUseCase.AddWalking(entity, _moveSpeed, _rotationSpeed);
            // Rigidbody-driven, authored in FixedTick. Enemies and bullets keep the
            // transform-writing TransformMoveBehaviour, which is correct for bodies they do not have.
            entity.AddBehaviour<RigidbodyMoveBehaviour>();
            entity.AddBehaviour<RigidbodyRotateBehaviour>();
            entity.AddBehaviour<CharacterVelocityBehaviour>();
            // Knocked-out pieces and rubble on the floor do not get in the way.
            entity.AddBehaviour(new PassThroughLoosePiecesBehaviour());

            // Attack
            CharacterAttackUseCase.AddHeldWeapon(entity, _weapon, _weaponRoot);
            entity.AddHandWeapon(new ReactiveVariable<IEntity>(_weapon));

            // Camera
            entity.AddCameraPoint(_cameraPoint);

            // Interaction
            _interactInstaller.Install(entity);
        }
    }
}