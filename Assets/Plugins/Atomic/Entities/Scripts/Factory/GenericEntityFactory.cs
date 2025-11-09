using System.Collections.Generic;
using SampleGame;

namespace Atomic.Entities
{
    public class GenericEntityFactory : GenericEntityFactory<string>, IGenericEntityFactory
    {
        public GenericEntityFactory()
        {
        }

        public GenericEntityFactory(ScriptableEntityCatalog catalog) :base(catalog.GetEntities())
        {
        }

        public GenericEntityFactory(IEnumerable<KeyValuePair<string, IEntityInstaller>> installers) : base(installers)
        {
        }

        public GenericEntityFactory(params KeyValuePair<string, IEntityInstaller>[] installers) : base(installers)
        {
        }

        protected override string GetName(string key)
        {
            return key;
        }
    }

    public class GenericEntityFactory<TKey> : IGenericEntityFactory<TKey>
    {
        private readonly Dictionary<TKey, IEntityInstaller> _installers;

        public GenericEntityFactory()
        {
            _installers = new Dictionary<TKey, IEntityInstaller>();
        }

        public GenericEntityFactory(IEnumerable<KeyValuePair<TKey, IEntityInstaller>> installers)
        {
            _installers = new Dictionary<TKey, IEntityInstaller>(installers);
        }

        public GenericEntityFactory(params KeyValuePair<TKey, IEntityInstaller>[] installers)
        {
            _installers = new Dictionary<TKey, IEntityInstaller>(installers);
        }

        public GenericEntityFactory<TKey> AddInstaller(TKey key, IEntityInstaller installer)
        {
            _installers.Add(key, installer);
            return this;
        }

        public GenericEntityFactory<TKey> RemoveInstaller(TKey key)
        {
            _installers.Remove(key);
            return this;
        }

        public IEntity Create(TKey key)
        {
            if (!_installers.TryGetValue(key, out IEntityInstaller installer))
                throw new KeyNotFoundException($"Can't create entity with key: {key}");

            string name = this.GetName(key);
            IEntity entity = new Entity(name);
            installer.Install(entity);
            return entity;
        }

        protected virtual string GetName(TKey key) => key.ToString();
    }
}