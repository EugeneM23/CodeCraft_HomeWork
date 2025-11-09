using System.Collections.Generic;
using UnityEngine;

namespace Atomic.Entities
{
    [AddComponentMenu("Atomic/Entities/Entity World View")]
    [DisallowMultipleComponent]
    public class EntityWorldView : MonoBehaviour
    {
        private readonly Dictionary<IEntity, EntityView> _activeViews = new();

        [SerializeField]
        private Transform _viewport;

        [SerializeField]
        private EntityViewPool _viewPool;

        private IEntityWorld _world;

        public void Show(IEntityWorld world)
        {
            this.Hide();

            _world = world;
            
            if (_world == null) 
                return;
            
            _world.OnAdded += this.SpawnView;
            _world.OnDeleted += this.UnspawnView;

            foreach (IEntity entity in _world.GetAll())
                this.SpawnView(entity);
        }

        public void Hide()
        {
            if (_world == null)
                return;

            _world.OnAdded -= this.SpawnView;
            _world.OnDeleted -= this.UnspawnView;

            foreach (IEntity entity in _world.GetAll())
                this.UnspawnView(entity);
        }

        protected virtual string GetEntityName(IEntity entity) => entity.Name;

        private void SpawnView(IEntity entity)
        {
            string name = this.GetEntityName(entity);
            EntityView view = _viewPool.Rent(name);
            view.transform.parent = _viewport;
            
            view.Show(entity);
            _activeViews.Add(entity, view);
        }

        private void UnspawnView(IEntity entity)
        {
            if (!_activeViews.Remove(entity, out EntityView view))
                return;

            view.Hide();
            string name = this.GetEntityName(entity);
            _viewPool.Return(name, view);
        }
    }
}