using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public sealed class EnemyCharacterCoreInstaller : MonoEntityInstaller
    {
        [Header("Combat Settings")] [SerializeField]
        private MonoEntity _weapon;
        [SerializeField] private Transform _weaponRoot;

        [Header("Movement Settings")] [SerializeField]
        private float _moveSpeed = 1;

        [SerializeField] private float _rotationSpeed = 15f;

        [Header("Health Settings")] [SerializeField]
        private int _health = 100;

        [Header("Core Components")] [SerializeField]
        private Transform _characterRoot;

        private GameContext _gameContext;

        public override void Install(IEntity entity)
        {
            _gameContext = GameContext.Instance;

            //Tags
            entity.AddDamageableTag();
            entity.AddEnemyTag();

            // Enemies are spawned through GameFactory, whose Destroy starts from this value, and
            // GetEntityID throws for a missing key. The id has to match the pool key, which is the
            // prefab name, so it is derived the same way the bullet and effect installers do it.
            entity.AddEntityID(gameObject.name.Replace("(Clone)", ""));

            // Core
            entity.AddGameObject(transform.gameObject);
            entity.AddTransform(_characterRoot);
            entity.AddTarget(new ReactiveVariable<IEntity>());

            // Health
            HealthUseCase.AddHealthWithEvents(entity, _health);
            entity.AddBehaviour(new DeathBehaviour(_gameContext));

            // Movement
            WalkingUseCase.AddWalking(entity, _moveSpeed, _rotationSpeed);
            entity.AddBehaviour<TransformMoveBehaviour>();
            entity.AddBehaviour<TransformRotateBehaviour>();
            entity.AddBehaviour<CharacterVelocityBehaviour>();
            // Knocked-out pieces and rubble on the floor do not get in the way.
            entity.AddBehaviour(new PassThroughLoosePiecesBehaviour());

            // Attack
            CharacterAttackUseCase.AddHeldWeapon(entity, _weapon, _weaponRoot);

            // No AI here: a plain enemy stands where it is placed. The elite's brain is AIKit
            // (Features/AI/EliteRegax, EliteRegaxInstaller), the horde's is Game.Horde.
        }
    }
}