using System.Collections.Generic;
using System.Linq;
using Atomic.Entities;
using UnityEngine;

namespace SampleGame
{
    [CreateAssetMenu(
        fileName = "EntityCatalog",
        menuName = "Atomic/Entities/New EntityCatalog"
    )]
    public sealed class ScriptableEntityCatalog : ScriptableObject
    {
        [SerializeField]
        private ScriptableEntityInstaller[] _entities;

        public IEnumerable<KeyValuePair<string, IEntityInstaller>> GetEntities()
        {
            return _entities.Select(it => new KeyValuePair<string, IEntityInstaller>(it.name, it));
        }
    }
}