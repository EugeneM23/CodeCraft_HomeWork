using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game.Gameplay
{
    public sealed class CharacterCoreInstaller : SceneEntityInstaller
    {
        [SerializeField] private float _moveSpeed = 15f;
        [SerializeField] private float _rotationSpeed = 15f;
        [SerializeField] private int _health = 100;
        [SerializeField] private WeaponEntity _weapon;

        public override void Install(IEntity entity)
        {
            entity.AddWeapon(_weapon);
            entity.AddGameObject(transform.gameObject);
            entity.AddTransform(transform);
            entity.AddRotationSpeed(new BaseVariable<float>(_rotationSpeed));
            entity.AddHealth(new ReactiveInt(_health));
            entity.AddMoveSpeed(new BaseFunction<float>(() => Mathf.Max(2f, _moveSpeed * (_health / 100f))));

            entity.AddMoveCondition(new AndExpression(entity.IsAlive));

            entity.AddMoveAction(new BaseAction<Vector3, float>((direction, deltaTime) =>
            {
                entity.Move(direction, deltaTime);
                entity.Rotate(direction, deltaTime);
            }));

            entity.AddBehaviour<DeathBehaviour>();
        }
    }
}