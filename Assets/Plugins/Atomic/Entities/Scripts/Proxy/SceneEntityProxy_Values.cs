using System;
using System.Collections.Generic;

namespace Atomic.Entities
{
    public partial class SceneEntityProxy<E>
    {
        public event Action<IEntity, int, object> OnValueAdded
        {
            add => _source.OnValueAdded += value;
            remove => _source.OnValueAdded -= value;
        }

        public event Action<IEntity, int, object> OnValueDeleted
        {
            add => _source.OnValueDeleted += value;
            remove => _source.OnValueDeleted -= value;
        }

        public event Action<IEntity, int, object> OnValueChanged
        {
            add => _source.OnValueChanged += value;
            remove => _source.OnValueChanged -= value;
        }

        public event Action<IEntity> OnValuesCleared
        {
            add => _source.OnValuesCleared += value;
            remove => _source.OnValuesCleared -= value;
        }
        
        public IReadOnlyDictionary<int, object> Values => _source.Values;

        public T GetValue<T>(in int id) => _source.GetValue<T>(id);
        public object GetValue(in int id) => _source.GetValue(id);
        public bool TryGetValue<T>(in int id, out T value) => _source.TryGetValue(in id, out value);
        public bool TryGetValue(in int id, out object value) => _source.TryGetValue(in id, out value);
        public bool AddValue(in int id, in object value) => _source.AddValue(in id, in value);
        public void SetValue(in int id, in object value) => _source.SetValue(in id, in value);
        public void SetValue(in int id, in object value, out object previous) => _source.SetValue(in id, in value, out previous);
        public bool DelValue(in int id) => _source.DelValue(in id);
        public bool DelValue(in int id, out object removed) => _source.DelValue(in id, out removed);
        public bool HasValue(in int id) => _source.HasValue(in id);
        public bool ClearValues() => _source.ClearValues();
    }
}