using Atomic.Entities;
using Modules.Gameplay;
using UnityEngine;

namespace Game.Gameplay
{
    public sealed class CharacterCoreInstaller : SceneEntityInstaller
    {
        [SerializeField] private float _moveSpeed = 15;

        public override void Install(IEntity entity)
        {
            entity.AddValue("Transform", transform);
            entity.AddValue("MoveSpeed", _moveSpeed);
        }
    }
}