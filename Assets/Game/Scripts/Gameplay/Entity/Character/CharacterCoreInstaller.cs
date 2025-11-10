using Atomic.Entities;
using Modules.Gameplay;
using UnityEngine;

namespace Game.Gameplay
{
    public sealed class CharacterCoreInstaller : SceneEntityInstaller
    {
        [SerializeField] private float _moveSpeed = 15f;
        [SerializeField] private float _rotationSpeed = 15f;

        public override void Install(IEntity entity)
        {
            entity.AddValue("Transform", transform);
            entity.AddValue("MoveSpeed", _moveSpeed);
            entity.AddValue("RotationSpeed", _rotationSpeed);
        }
    }
}