using System;
using UnityEngine;
using Zenject;

namespace Modules.Entities
{
    //Don't modify
    [RequireComponent(typeof(Transform))]
    public sealed class Entity : MonoBehaviour
    {
        public string Name => _config ? _config.Name : string.Empty;
        public EntityType Type => _config ? _config.Type : EntityType.None;

        [field: SerializeField] public int Id { get; internal set; }

        [SerializeField] internal EntityConfig _config;
        [SerializeField] private GameObjectContext _context;

        public DiContainer Context => _context.Container;
    }
}