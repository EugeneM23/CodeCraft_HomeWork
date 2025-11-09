using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Sirenix.OdinInspector;
using UnityEngine;

// ReSharper disable ConditionIsAlwaysTrueOrFalse
// ReSharper disable HeuristicUnreachableCode
// ReSharper disable AssignNullToNotNullAttribute

namespace Atomic.Elements
{
    //TODO: СДЕЛАТЬ LOCK НА ПОДПИСКУ
    //TODO: Не работает! Пока не брать :)
    public partial class ReactiveHashSet<T> : IReactiveSet<T>, IDisposable
    {
        private const int UNDEFINED_INDEX = -1;
        private static readonly IEqualityComparer<T> s_comparer = EqualityComparer.GetDefault<T>();

        private struct Slot
        {
            public T value;
            public int next;
            public bool exists;
        }

        public event StateChangedHandler OnStateChanged;
        public event AddItemHandler<T> OnItemAdded;
        public event RemoveItemHandler<T> OnItemRemoved;

        public int Capacity => _slots.Length;
        public int Count => _count;
        public bool IsReadOnly => false;

        private Slot[] _slots;
        private int _count;

        private int[] _buckets;
        private int _freeList;

        //+
        public ReactiveHashSet(int capacity = 0)
        {
            if (capacity < 0)
                throw new ArgumentOutOfRangeException(nameof(capacity));

            _slots = new Slot[capacity];
            _buckets = new int[capacity];

            this.ResetInternal();
        }

        //+
        public ReactiveHashSet(params T[] elements) : this(elements.Length)
        {
            this.UnionWith(elements);
        }

        //+
        public ReactiveHashSet(IReadOnlyCollection<T> elements) : this(elements.Count)
        {
            this.UnionWith(elements);
        }

        //+
        public ReactiveHashSet(IEnumerable<T> elements) : this(elements.Count())
        {
            this.UnionWith(elements);
        }

        //+
        public bool Contains(T item)
        {
            return item != null && this.FindIndex(item, out _);
        }

        //+
        public bool IsEmpty()
        {
            return _count == 0;
        }

        //+
        public bool IsNotEmpty()
        {
            return _count > 0;
        }

        //+
        public bool Add(T item)
        {
            if (item == null)
                return false;

            if (!this.AddInternal(item, out _))
                return false;

            this.OnItemAdded?.Invoke(item);
            this.OnStateChanged?.Invoke();
            return true;
        }

        //+
        public void UnionWith(IEnumerable<T> other)
        {
            if (other == null)
                throw new ArgumentNullException(nameof(other));

            int prevCount = _count;

            foreach (T item in other)
                if (item != null && this.AddInternal(item, out _))
                    this.OnItemAdded?.Invoke(item);

            if (prevCount < _count)
                this.OnStateChanged?.Invoke();
        }
        
        //??? ОСТАНОВИЛСЯ ТУТ!
        public void Clear()
        {
            if (_count == 0)
                return;

            _count = 0;
            _freeList = UNDEFINED_INDEX;

            for (int i = 0, capacity = _slots.Length; i < capacity; i++)
            {
                _buckets[i] = UNDEFINED_INDEX;

                ref Slot slot = ref _slots[i];
                if (!slot.exists)
                    continue;

                slot.exists = false;
                slot.next = UNDEFINED_INDEX;
                this.OnItemRemoved?.Invoke(slot.value);
            }

            this.OnStateChanged?.Invoke();
        }

        public bool Remove(T item)
        {
            if (_count == 0)
                return false;

            if (!this.RemoveInternal(item))
                return false;

            this.OnItemRemoved?.Invoke(item);
            this.OnStateChanged?.Invoke();
            return true;
        }

       

        public void Dispose()
        {
            this.Clear();

            AtomicHelper.Dispose(ref OnItemAdded);
            AtomicHelper.Dispose(ref OnItemRemoved);
            AtomicHelper.Dispose(ref OnStateChanged);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            for (int i = 0, length = _slots.Length; i < length; i++)
            {
                Slot slot = _slots[i];
                if (slot.exists)
                    array[arrayIndex++] = slot.value;
            }
        }

        public void CopyTo(T[] array)
        {
            int index = 0;
            for (int i = 0, capacity = _slots.Length; i < capacity; i++)
            {
                Slot slot = _slots[i];
                if (slot.exists)
                    array[index++] = slot.value;
            }
        }

        void ICollection<T>.Add(T item)
        {
            this.Add(item);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return new Enumerator(this);
        }

        public IEnumerator<T> GetEnumerator()
        {
            return new Enumerator(this);
        }


        public void ExceptWith(IEnumerable<T> other)
        {
            if (other == null)
                throw new ArgumentNullException(nameof(other));

            if (_count == 0)
                return;

            int count = _count;

            foreach (T item in other)
                if (this.RemoveInternal(item))
                    this.OnItemRemoved?.Invoke(item);

            if (count > _count)
                this.OnStateChanged?.Invoke();
        }

        public unsafe void IntersectWith(IEnumerable<T> other)
        {
            if (other == null)
                throw new ArgumentNullException(nameof(other));

            if (_count == 0)
                return;

            int capacity = _slots.Length;
            bool* intersectedItems = stackalloc bool[capacity];
            foreach (T item in other)
            {
                this.FindIndex(item, out int index);
                intersectedItems[index] = index >= 0;
            }

            int count = _count;

            for (int i = 0; i < capacity; i++)
            {
                Slot slot = _slots[i];
                if (slot.exists && !intersectedItems[i])
                {
                    this.RemoveInternal(slot.value);
                    this.OnItemRemoved?.Invoke(slot.value);
                }
            }

            if (count > _count)
                this.OnStateChanged?.Invoke();
        }

        public unsafe void SymmetricExceptWith(IEnumerable<T> other)
        {
            if (other == null)
                throw new ArgumentNullException(nameof(other));

            int count = _count;
            int capacity = _slots.Length;

            bool* itemsToRemove = stackalloc bool[capacity];
            bool* itemsAdded = stackalloc bool[capacity];

            foreach (T item in other)
            {
                bool added = this.AddInternal(item, out int index);
                if (added)
                {
                    itemsAdded[index] = true;
                    this.OnItemAdded?.Invoke(item);
                }
                else if (!itemsAdded[index])
                {
                    itemsToRemove[index] = true;
                }
            }

            for (int i = 0; i < capacity; i++)
            {
                if (!itemsToRemove[i])
                    continue;

                Slot slot = _slots[i];
                if (!slot.exists)
                    continue;

                T value = slot.value;
                this.RemoveInternal(value);
                this.OnItemRemoved?.Invoke(value);
            }

            if (count != _count)
                this.OnStateChanged?.Invoke();
        }

        public bool IsProperSubsetOf(IEnumerable<T> other)
        {
            throw new NotImplementedException();
        }

        public bool IsProperSupersetOf(IEnumerable<T> other)
        {
            throw new NotImplementedException();
        }

        public bool IsSubsetOf(IEnumerable<T> other)
        {
            throw new NotImplementedException();
        }

        public bool IsSupersetOf(IEnumerable<T> other)
        {
            throw new NotImplementedException();
        }

        public bool Overlaps(IEnumerable<T> other)
        {
            throw new NotImplementedException();
        }

        public bool SetEquals(IEnumerable<T> other)
        {
            throw new NotImplementedException();
        }


        private void Resize()
        {
            Slot[] prevSlots = _slots;
            int prevCapacity = _slots.Length;

            int newCapacity = AtomicHelper.NextPrime(prevCapacity);
            _slots = new Slot[newCapacity];
            _buckets = new int[newCapacity];

            this.ResetInternal();

            for (int i = 0; i < prevCapacity; i++)
            {
                Slot prevSlot = prevSlots[i];
                if (!prevSlot.exists)
                    continue;

                T item = prevSlot.value;
                ref int bucket = ref this.Bucket(item);

                _slots[_count] = new Slot
                {
                    value = item,
                    next = bucket,
                    exists = true
                };

                bucket = _count++;
            }
        }

        private bool RemoveInternal(T item)
        {
            ref int bucket = ref this.Bucket(item);

            int index = bucket;
            int last = UNDEFINED_INDEX;

            while (index >= 0)
            {
                ref Slot node = ref _slots[index];
                if (s_comparer.Equals(node.value, item))
                {
                    if (last == UNDEFINED_INDEX)
                        bucket = node.next;
                    else
                        _slots[last].next = node.next;

                    node.next = _freeList;
                    node.exists = false;

                    _count--;
                    _freeList = _count == 0 ? UNDEFINED_INDEX : index;
                    return true;
                }

                last = index;
                index = node.next;
            }

            return false;
        }

        private bool AddInternal(T item, out int index)
        {
            if (this.FindIndex(item, out index))
                return false;

            if (_count == _slots.Length)
                this.Resize();

            if (_freeList == UNDEFINED_INDEX)
            {
                index = _count;
            }
            else
            {
                index = _freeList;
                _freeList = _slots[_freeList].next;
            }

            ref int bucket = ref this.Bucket(item);

            _slots[index] = new Slot
            {
                value = item,
                next = bucket,
                exists = true
            };

            bucket = index;

            _count++;
            return true;
        }

        private bool FindIndex(T item, out int index)
        {
            if (_count == 0)
            {
                index = UNDEFINED_INDEX;
                return false;
            }

            index = this.Bucket(item);
            while (index >= 0)
            {
                Slot slot = _slots[index];
                if (slot.exists && s_comparer.Equals(slot.value, item))
                    return true;

                index = slot.next;
            }

            return false;
        }

        private ref int Bucket(T item)
        {
            int index = (int) ((uint) item.GetHashCode() % _slots.Length);
            return ref _buckets[index];
        }

        private void ResetInternal()
        {
            _count = 0;
            _freeList = UNDEFINED_INDEX;

            for (int i = 0, capacity = _slots.Length; i < capacity; i++)
            {
                _buckets[i] = UNDEFINED_INDEX;

                _slots[i] = new Slot
                {
                    exists = false,
                    next = UNDEFINED_INDEX,
                    value = default
                };
            }
        }


        private struct Enumerator : IEnumerator<T>
        {
            private readonly ReactiveHashSet<T> _set;
            private int _index;
            private T _current;

            public T Current => _current;
            object IEnumerator.Current => _current;

            public Enumerator(ReactiveHashSet<T> set)
            {
                _set = set;
                _index = 0;
                _current = default;
            }

            public bool MoveNext()
            {
                int capacity = _set._slots.Length;
                while (_index < capacity)
                {
                    ref Slot slot = ref _set._slots[_index++];
                    if (slot.exists)
                    {
                        _current = slot.value;
                        return true;
                    }
                }

                _index = capacity;
                _current = default;
                return false;
            }

            void IEnumerator.Reset()
            {
                _index = 0;
                _current = default;
            }

            public void Dispose()
            {
                //Do nothing...
            }
        }
    }
}