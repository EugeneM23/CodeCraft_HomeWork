using Atomic.Elements;
using Atomic.Entities;
using Game.Scripts.Gameplay;
using Modules.Gameplay;
using SampleGame;
using UnityEngine;

namespace Game.Gameplay
{
    public sealed class CharacterCoreInstaller : SceneEntityInstaller
    {
        [SerializeField] private float _moveSpeed = 15f;
        [SerializeField] private float _rotationSpeed = 15f;
        [SerializeField] private int _health = 100;

        public override void Install(IEntity entity)
        {
            entity.AddGameObject(transform.gameObject);
            entity.AddTransform(transform);
            entity.AddRotationSpeed(new BaseVariable<float>(_rotationSpeed));
            entity.AddHealth(new ReactiveInt(_health));

            entity.AddMoveSpeed(new BaseFunction<float>(() => Mathf.Max(2f, _moveSpeed * (_health / 100f))));

            entity.AddMoveAction(new BaseAction<Vector3, float>((direction, deltaTime) =>
            {
                entity.Move(direction, deltaTime);
                entity.Rotate(direction, deltaTime);
            }));

            entity.AddBehaviour<DeathBehaviour>();
        }
    }
}