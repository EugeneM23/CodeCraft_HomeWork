using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Atomic.Elements
{
    [Serializable]
    public partial class ReactiveHashSet<T> : ISerializationCallbackReceiver
    {
        [SerializeField, HideInPlayMode]
        private T[] serializedItems;

        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
            this.CopyTo(this.serializedItems);
        }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            int capacity = this.serializedItems.Length;
            _slots = new Slot[capacity];
            _buckets = new int[capacity];
            this.ResetInternal();
            this.UnionWith(this.serializedItems);
        }
    }
}
