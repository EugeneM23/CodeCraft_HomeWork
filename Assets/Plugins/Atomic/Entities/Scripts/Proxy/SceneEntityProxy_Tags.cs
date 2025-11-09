using System;
using System.Collections.Generic;

namespace Atomic.Entities
{
    public partial class SceneEntityProxy<E>
    {
        public event Action<IEntity, int> OnTagAdded
        {
            add => _source.OnTagAdded += value;
            remove => _source.OnTagAdded -= value;
        }

        public event Action<IEntity, int> OnTagDeleted
        {
            add => _source.OnTagDeleted += value;
            remove => _source.OnTagDeleted -= value;
        }

        public event Action<IEntity> OnTagsCleared
        {
            add => _source.OnTagsCleared += value;
            remove => _source.OnTagsCleared -= value;
        }

        public IReadOnlyCollection<int> Tags => _source.Tags;

        public bool HasTag(in int tag) => _source.HasTag(in tag);
        public bool AddTag(in int tag) => _source.AddTag(in tag);
        public bool DelTag(in int tag) => _source.DelTag(in tag);
        public bool ClearTags() => _source.ClearTags();
    }
}