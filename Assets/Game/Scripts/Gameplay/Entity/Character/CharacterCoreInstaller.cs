using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game.Gameplay
{
    public sealed class CharacterCoreInstaller : SceneEntityInstaller
    {
        [Header("Animation")] [SerializeField] private Animator _animator;

        [Header("Movement")] [SerializeField] private float _moveSpeed = 15f;
        [SerializeField] private float _rotationSpeed = 15f;

        [Header("Health")] [SerializeField] private int _health = 100;

        [Header("Combat")] [SerializeField] private SceneEntity _weapon;

        public override void Install(IEntity entity)
        {
            // 🧩 Core
            entity.AddGameObject(transform.gameObject);
            entity.AddTransform(transform);
            entity.AddDamageableTag();

            // 🎬 Animation
            entity.AddAnimator(_animator);

            // ❤️ Health
            entity.AddHealth(new ReactiveInt(_health));

            // 🌀 Movement
            entity.AddRotationSpeed(new BaseVariable<float>(_rotationSpeed));
            entity.AddMoveSpeed(new BaseFunction<float>(() => Mathf.Max(2f, _moveSpeed * (_health / 100f))));
            entity.AddMoveCondition(new AndExpression(entity.IsAlive));
            entity.AddMoveDirection(new ReactiveVariable<Vector3>());

            // ⚔️ Combat
            entity.AddWeapon(_weapon);
            entity.AddFireEvent(new BaseEvent());
            entity.AddFireCondition(new BaseFunction<bool>(entity.IsAlive));
            
            entity.AddFireAction(new BaseAction(() =>
            {
                if (entity.GetFireCondition().Invoke())
                {
                    FireUseCase.Fire(entity.GetWeapon());
                    entity.GetFireEvent().Invoke();
                }
            }));

            // ⚙️ Behaviours 
            entity.AddBehaviour<DeathBehaviour>();
            entity.AddBehaviour<MoveAnimBehaviour>();
            entity.AddBehaviour<FireAnimBehaviour>();
            entity.AddBehaviour<MoveBehaviour>();
            entity.AddBehaviour<RotationBehaviour>();
        }
    }
}