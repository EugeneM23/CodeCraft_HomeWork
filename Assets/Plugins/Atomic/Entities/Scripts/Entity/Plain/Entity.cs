using System;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Atomic.Entities
{
    public partial class Entity : IEntity
    {
        private static readonly Dictionary<int, IEntity> s_entities = new();
        private static int s_maxId = -1;

        public int Id
        {
            get { return this.id; }
            set { this.SetId(value); }
        }

        public string Name
        {
            get { return this.name; }
            set { this.name = value; }
        }

        private readonly IEntity owner;
        private string name;
        private int id;

        public Entity(
            in string name = null,
            in IEnumerable<int> tags = null,
            in IDictionary<int, object> values = null,
            in IEnumerable<IEntityBehaviour> behaviours = null,
            in IEntity owner = null,
            in int id = -1
        )
        {
            this.name = name ?? string.Empty;
            this.id = id < 0 ? NextId() : id;

            this.tags = tags == null
                ? new HashSet<int>()
                : new HashSet<int>(tags);

            this.values = values == null
                ? new Dictionary<int, object>()
                : new Dictionary<int, object>(values);
            this.behaviours = behaviours == null
                ? new HashSet<IEntityBehaviour>()
                : new HashSet<IEntityBehaviour>(behaviours);

            this.owner = owner ?? this;
        }

        public void UnsubscribeAll()
        {
            DelegateUtils.Unsubscribe(ref this.OnInitialized);
            DelegateUtils.Unsubscribe(ref this.OnEnabled);
            DelegateUtils.Unsubscribe(ref this.OnDisabled);
            DelegateUtils.Unsubscribe(ref this.OnUpdated);
            DelegateUtils.Unsubscribe(ref this.OnFixedUpdated);
            DelegateUtils.Unsubscribe(ref this.OnLateUpdated);
            DelegateUtils.Unsubscribe(ref this.OnDisposed);
        }

        public override string ToString()
        {
            return $"{nameof(name)}: {name}, {nameof(id)}: {id}";
        }

        public bool Equals(IEntity other)
        {
            return this.id == other.Id;
        }

        public override bool Equals(object obj)
        {
            return obj is IEntity other && other.Id == this.id;
        }

        public override int GetHashCode()
        {
            return this.id;
        }

        public void Clear()
        {
            this.ClearTags();
            this.ClearValues();
            this.ClearBehaviours();
        }

        private static int NextId()
        {
            do s_maxId++;
            while (s_entities.ContainsKey(s_maxId));
            return s_maxId;
        }

        private void SetId(int id)
        {
            if (id < 0)
                throw new Exception($"Entity Id cannot be negative! Actual: {id}!");

            s_entities.Remove(this.id);
            s_entities[id] = this;

            this.id = id;
        }

        public static bool Find(int id, out IEntity entity)
        {
            return s_entities.TryGetValue(id, out entity);
        }

#if UNITY_EDITOR
        [InitializeOnEnterPlayMode]
#endif
        public static void ResetAll()
        {
            s_maxId = -1;
            s_entities.Clear();
        }
    }
}