using System;
using System.Collections.Generic;
using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public class CharacterBuffInstaller : MonoEntityInstaller
    {
        /// <summary>
        /// One buff name paired with the particle system that marks it on the character.
        /// </summary>
        [Serializable]
        private struct BuffParticles
        {
            [Tooltip("BuffConfig.Name of the buff this particle system belongs to.")]
            public string name;

            public ParticleSystem particles;
        }

        /// <remarks>
        /// This used to be an Odin <c>Dictionary&lt;string, ParticleSystem&gt;</c>, which worked
        /// because the old <c>SceneEntityInstaller</c> derived from Odin's
        /// <c>SerializedMonoBehaviour</c>. Its replacement, <c>MonoEntityInstaller</c>, is a plain
        /// <c>MonoBehaviour</c>, so <c>[OdinSerialize]</c> on this class became dead: Odin stopped
        /// both writing and reading the field, the mapping went empty, and the buff status
        /// particles stopped appearing. Upstream ships no Odin-serialized installer to derive from
        /// and the base class is not ours to change, so the mapping is plain Unity serialization
        /// now — a shape that does not depend on which base class the framework happens to use.
        /// </remarks>
        [SerializeField]
        private BuffParticles[] _buffsParticles = Array.Empty<BuffParticles>();

        public override void Install(IEntity entity)
        {
            // On Zombie.prefab EnemyCoreInstaller adds BuffsEffects too; first one wins, as before.
            if (!entity.HasBuffsEffects())
                entity.AddBuffsEffects(new ReactiveList<BuffBase>());

            entity.AddBehaviour<DiscardAllBuffBehaviour>();
            entity.AddBehaviour(new BuffVFXBehaviour(this.BuildParticleMap()));
        }

        private Dictionary<string, ParticleSystem> BuildParticleMap()
        {
            var map = new Dictionary<string, ParticleSystem>(_buffsParticles.Length);

            foreach (BuffParticles entry in _buffsParticles)
            {
                if (string.IsNullOrEmpty(entry.name) || entry.particles == null)
                    continue;

                map[entry.name] = entry.particles;
            }

            return map;
        }
    }
}
