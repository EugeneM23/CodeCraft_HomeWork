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
            // On Zombie.prefab EnemyCoreInstaller adds BuffsEffects too; first one wins, as before.
            if (!entity.HasBuffsEffects())
                entity.AddBuffsEffects(new ReactiveList<BuffBase>());
            entity.AddBehaviour<DiscardAllBuffBehaviour>();
            entity.AddBehaviour(new BuffVFXBehaviour(_buffsParticles));
        }
    }
}