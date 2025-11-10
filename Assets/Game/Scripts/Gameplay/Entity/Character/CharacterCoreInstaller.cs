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

        public override void Install(IEntity entity)
        {
            entity.AddTransform(transform);
            entity.AddMoveSpeed(_moveSpeed);
            entity.AddRotationSpeed(_rotationSpeed);

            entity.AddMoveAction((direction, deltaTime) =>
            {
                entity.Move(direction, deltaTime);
                entity.Rotate(direction, deltaTime);
            });
        }
    }
}