using Atomic.Elements;
using Atomic.Entities;
using Modules.Gameplay;
using UnityEngine;

namespace Game.Gameplay
{
    public sealed class CharacterCoreInstaller : SceneEntityInstaller
    {
        [SerializeField] private float _moveSpeed = 15f;
        [SerializeField] private float _rotationSpeed = 15f;
        [SerializeField] private Health _health;
        [SerializeField] private SceneEntity _weapon;
        [SerializeField] private TriggerEventReceiver _triggerReceiver;
        [SerializeField] private InteractInstaller _interactInstaller;
        [SerializeField] private Transform _weaponRoot;
        [SerializeField] private Rigidbody _rb;
        [SerializeField] private Animator _animator;

        [SerializeField] private RuntimeAnimatorController _fistController;
        [SerializeField] private RuntimeAnimatorController _weaponController;

        public override void Install(IEntity entity)
        {
            // 🧩 Core
            entity.AddRiggedBody(_rb);
            entity.AddTriggerEventReceiver(_triggerReceiver);
            entity.AddGameObject(transform.gameObject);
            entity.AddTransform(transform);
            entity.AddDamageableTag();

            // 🎬 Animation
            entity.AddAnimator(_animator);

            // ❤️ Health
            entity.AddHealth(_health);

            // 🌀 Movement
            entity.AddRotationSpeed(new BaseVariable<float>(_rotationSpeed));
            entity.AddMoveSpeed(new Const<float>(_moveSpeed));
            entity.AddMoveCondition(new AndExpression(entity.GetHealth().Exists));
            entity.AddMoveDirection(new ReactiveVariable<Vector3>());

            // ⚔️ Combat
            entity.AddWeapon(new ReactiveVariable<IEntity>(_weapon));
            entity.AddWeaponRoot(_weaponRoot);
            entity.AddFireCondition(new AndExpression(entity.GetHealth().Exists));

            // ⚙️ Behaviours  
            entity.AddBehaviour<DeathBehaviour>();
            entity.AddBehaviour<CharacterMoveBehaviour>();
            entity.AddBehaviour<FireAnimBehaviour>();
            entity.AddBehaviour<CharacterMoveAnimBehaviour>();
            entity.AddBehaviour(new CharacterSwitchAnimGraphBehaviour(_fistController, _weaponController));

            // 🛠️ Interact
            _interactInstaller.Install(entity);
        }
    }
}