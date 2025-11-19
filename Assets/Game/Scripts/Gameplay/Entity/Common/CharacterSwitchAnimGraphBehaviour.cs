using System;
using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public class CharacterSwitchAnimSetBehaviour : IEntityInit, IEntityUpdate
    {
        private IReactiveVariable<IEntity> _weapom;

        private Animator _animator;

        public void Init(in IEntity entity)
        {
            
        }

        private void OnWeaponChanged(IEntity weapon)
        {
        }

        public void OnUpdate(in IEntity entity, in float deltaTime)
        {
            if (Input.GetKeyDown(KeyCode.L))
            {
            }
        }
    }
}