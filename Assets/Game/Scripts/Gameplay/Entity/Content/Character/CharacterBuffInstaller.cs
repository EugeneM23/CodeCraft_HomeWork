using System.Collections.Generic;
using Atomic.Elements;
using Atomic.Entities;
using Sirenix.Serialization;
using UnityEngine;

namespace Game
{
    public class CharacterBuffInstaller : MonoEntityInstaller
    {
        [OdinSerialize]
        private Dictionary<string, ParticleSystem> _buffsParticles = new();

        public override void Install(IEntity entity)
        {
            entity.AddBuffsEffects(new ReactiveList<BuffBase>());
            entity.AddBehaviour<DiscardAllBuffBehaviour>();
            entity.AddBehaviour(new BuffVFXBehaviour(_buffsParticles));
        }
    }
}