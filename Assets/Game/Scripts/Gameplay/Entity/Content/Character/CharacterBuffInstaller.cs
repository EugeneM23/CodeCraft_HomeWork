using System.Collections.Generic;
using Atomic.Elements;
using Atomic.Entities;
using Sirenix.Serialization;
using UnityEngine;

namespace Game
{
    public class CharacterBuffInstaller : SceneEntityInstaller
    {
        [OdinSerialize] private Dictionary<BaseBuff, ParticleSystem> _buffsParticles = new();

        public override void Install(IEntity entity)
        {
            entity.AddBuffsEffects(new ReactiveList<BaseBuff>());
            entity.AddBehaviour<DiscardAllBuffBehaviour>();
            //entity.AddBehaviour(new BuffVFXBehaviour());
        }
    }
}