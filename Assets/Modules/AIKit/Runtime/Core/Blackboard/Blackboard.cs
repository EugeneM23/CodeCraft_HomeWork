using System;
using System.Collections.Generic;

namespace AIKit
{
    /// <summary>Where a variable lives: on the agent, on its squad or in the world.</summary>
    public enum BlackboardScope
    {
        Agent,
        Squad,
        Global
    }

    /// <summary>
    /// Typed facts of one scope: the only channel between the layers (senses write, the graph reads, tasks write back).
    /// A slot is created by the first write of a name (or Declare) and keeps its index until Clear, so a key resolved
    /// once stays valid — Remove empties the slot instead of deleting it. Reading never declares: an unknown name gives
    /// the fallback and leaves the blackboard as it was. Every change of a value — the first write, a different value,
    /// Remove — bumps the slot's version and raises Changed with the slot index; transitions by variable change and the
    /// debugger listen to it. Nodes read through BlackboardVariable, which hashes a name once, not on every read.
    /// </summary>
    public sealed class Blackboard
    {
        /// <summary>The value of a tag slot: a tag has no value, only presence.</summary>
        private struct TagMark : IEquatable<TagMark>
        {
            public bool Equals(TagMark other) => true;
        }

        private interface ISlot
        {
            string Name { get; }

            Type ValueType { get; }

            int Version { get; }

            bool HasValue { get; }

            object Boxed { get; }

            void Bump();

            void Empty();
        }

        private sealed class Slot<T> : ISlot
        {
            public string SlotName;
            public T Value;
            public bool Present;
            public int Revision;

            string ISlot.Name => SlotName;

            Type ISlot.ValueType => typeof(T);

            int ISlot.Version => Revision;

            bool ISlot.HasValue => Present;

            object ISlot.Boxed => Present ? Value : null;

            void ISlot.Bump() => Revision++;

            void ISlot.Empty()
            {
                Present = false;
                Value = default;
            }
        }

        private readonly Dictionary<string, int> _indices = new();
        private readonly List<ISlot> _slots = new();

        // Name → id is one table for the whole process (like Shader.PropertyToID): a node hashes a name once, ever, and
        // then finds the slot of any agent's blackboard by an array index. Only names are shared, never values.
        private static readonly Dictionary<string, int> NameIds = new();
        private int[] _slotById = Array.Empty<int>();             // id → slot index + 1 (0: none)

        /// <summary>The process-wide id of a variable name (made on first ask); -1 for an empty name.</summary>
        public static int NameToId(string name)
        {
            if (string.IsNullOrEmpty(name))
                return -1;

            lock (NameIds)
            {
                if (!NameIds.TryGetValue(name, out int id))
                {
                    id = NameIds.Count;
                    NameIds.Add(name, id);
                }

                return id;
            }
        }

        /// <summary>Slot index of a name id; -1 — no such variable on this blackboard. No hashing.</summary>
        public int IndexOfId(int id) => id >= 0 && id < _slotById.Length ? _slotById[id] - 1 : -1;

        /// <summary>The slot at index holds a T (a key of that type may read it).</summary>
        public bool IsOfType<T>(int index) => index >= 0 && index < _slots.Count && _slots[index] is Slot<T>;

        /// <summary>Index of the slot that has just changed.</summary>
        public event Action<int> Changed;

        /// <summary>Slots, removed ones included (indices run from 0 to Count - 1).</summary>
        public int Count => _slots.Count;

        /// <summary>Grows when a slot is added or the blackboard is cleared: a cached "not found" is then out of date.</summary>
        public int Layout { get; private set; }

        /// <summary>Grows on Clear: every key resolved before it is void.</summary>
        public int Generation { get; private set; }

        /// <summary>
        /// Declares a variable with a value (or returns the key of the one already declared under this name; a removed
        /// variable gets the value back, a present one keeps its own).
        /// </summary>
        public BlackboardKey<T> Declare<T>(string name, T initial = default)
        {
            BlackboardKey<T> key = SlotFor<T>(name, out bool created);
            if (created || !((Slot<T>)_slots[key.Index]).Present)
                Write((Slot<T>)_slots[key.Index], key.Index, initial);

            return key;
        }

        public TagKey DeclareTag(string name) => new(SlotFor<TagMark>(name, out _).Index);

        public bool TryGetKey<T>(string name, out BlackboardKey<T> key)
        {
            if (name != null && _indices.TryGetValue(name, out int index) && _slots[index] is Slot<T>)
            {
                key = new BlackboardKey<T>(index);
                return true;
            }

            key = BlackboardKey<T>.None;
            return false;
        }

        /// <summary>Slot index of a name, whatever its type; -1 — no such variable (debugger, "variable changed" edges).</summary>
        public int IndexOf(string name) => name != null && _indices.TryGetValue(name, out int index) ? index : -1;

        public bool TryGetTagKey(string name, out TagKey key)
        {
            bool found = TryGetKey(name, out BlackboardKey<TagMark> slot);
            key = new TagKey(slot.Index);
            return found;
        }

        /// <summary>The variable exists and holds a value (a removed one does not).</summary>
        public bool Has(string name) =>
            name != null && _indices.TryGetValue(name, out int index) && _slots[index].HasValue;

        public bool Has<T>(BlackboardKey<T> key) => SlotAt(key) is { Present: true };

        public bool TryGet<T>(BlackboardKey<T> key, out T value)
        {
            if (SlotAt(key) is { Present: true } slot)
            {
                value = slot.Value;
                return true;
            }

            value = default;
            return false;
        }

        public T Get<T>(BlackboardKey<T> key, T fallback = default) =>
            SlotAt(key) is { Present: true } slot ? slot.Value : fallback;

        /// <summary>
        /// Reads by name; a variable that does not exist gives the fallback and is not declared. A number is read as any number
        /// (an int variable read as a float, a double as an int — rounded).
        /// </summary>
        public T Get<T>(string name, T fallback = default)
        {
            if (TryGetKey(name, out BlackboardKey<T> key))
                return Get(key, fallback);

            return Numbers.Is<T>() && name != null && _indices.TryGetValue(name, out int index) &&
                   TryGetNumberAt(index, out double number)
                ? Numbers.From<T>(number)
                : fallback;
        }

        public void Set<T>(BlackboardKey<T> key, T value)
        {
            if (SlotAt(key) is not { } slot)
                return;

            if (slot.Present && EqualityComparer<T>.Default.Equals(slot.Value, value))
                return;

            Write(slot, key.Index, value);
        }

        /// <summary>
        /// Writes by name; the first write creates the variable and is a change like any other. A number written into a
        /// number variable of another type is kept in the variable's own type (an int rounds).
        /// </summary>
        public void Set<T>(string name, T value)
        {
            if (Numbers.Is<T>() && name != null && _indices.TryGetValue(name, out int index) &&
                _slots[index] is not Slot<T> && SetNumberAt(index, Numbers.To(value)))
                return;

            Set(SlotFor<T>(name, out _), value);
        }

        /// <summary>
        /// The number a variable holds, whatever its number type (int, float, double — 15.11.2026: every number of the graph
        /// reads and writes any of them); false — not a number, or empty.
        /// </summary>
        public bool TryGetNumberAt(int index, out double value)
        {
            value = 0d;
            if (index < 0 || index >= _slots.Count)
                return false;

            switch (_slots[index])
            {
                case Slot<float> { Present: true } single:
                    value = single.Value;
                    return true;
                case Slot<int> { Present: true } whole:
                    value = whole.Value;
                    return true;
                case Slot<double> { Present: true } wide:
                    value = wide.Value;
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>The variable is of a number type (int, float, double), filled or not.</summary>
        public bool IsNumberAt(int index) =>
            index >= 0 && index < _slots.Count && _slots[index] is Slot<float> or Slot<int> or Slot<double>;

        /// <summary>Writes a number into a number variable in its own type (an int rounds); false — not a number variable.</summary>
        public bool SetNumberAt(int index, double value)
        {
            if (index < 0 || index >= _slots.Count)
                return false;

            switch (_slots[index])
            {
                case Slot<float>:
                    Set(new BlackboardKey<float>(index), (float)value);
                    return true;
                case Slot<int>:
                    Set(new BlackboardKey<int>(index), (int)Math.Round(value, MidpointRounding.AwayFromZero));
                    return true;
                case Slot<double>:
                    Set(new BlackboardKey<double>(index), value);
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Empties the slot (the key stays valid, a later write brings the variable back); false — nothing to remove.</summary>
        public bool Remove<T>(BlackboardKey<T> key)
        {
            if (SlotAt(key) is not { Present: true } slot)
                return false;

            ((ISlot)slot).Empty();
            MarkChanged(slot, key.Index);
            return true;
        }

        public bool Remove(string name)
        {
            if (name == null || !_indices.TryGetValue(name, out int index) || !_slots[index].HasValue)
                return false;

            ISlot slot = _slots[index];
            slot.Empty();
            MarkChanged(slot, index);
            return true;
        }

        public bool HasTag(TagKey key) => Has(new BlackboardKey<TagMark>(key.Index));

        public bool HasTag(string name) => TryGetKey(name, out BlackboardKey<TagMark> key) && Has(key);

        public void SetTag(TagKey key) => Set(new BlackboardKey<TagMark>(key.Index), default(TagMark));

        public void SetTag(string name) => Set(name, default(TagMark));

        public bool RemoveTag(TagKey key) => Remove(new BlackboardKey<TagMark>(key.Index));

        public bool RemoveTag(string name) => TryGetKey(name, out BlackboardKey<TagMark> key) && Remove(key);

        /// <summary>
        /// The value by reference: change a field of a struct in place, without copying the slot. The write is silent —
        /// call NotifyChanged when listeners must hear it. A removed variable is brought back (with its default value).
        /// </summary>
        public ref T Ref<T>(BlackboardKey<T> key)
        {
            if (SlotAt(key) is not { } slot)
                throw new ArgumentException($"The key does not point at a {typeof(T).Name} variable of this blackboard");

            slot.Present = true;
            return ref slot.Value;
        }

        /// <summary>Raises Changed for a slot written through Ref (and bumps its version).</summary>
        public void NotifyChanged(int index)
        {
            if (index >= 0 && index < _slots.Count)
                MarkChanged(_slots[index], index);
        }

        /// <summary>How many times the slot changed: a transition by change compares the number it saw.</summary>
        public int VersionOf(int index) => index >= 0 && index < _slots.Count ? _slots[index].Version : 0;

        public string NameOf(int index) => index >= 0 && index < _slots.Count ? _slots[index].Name : null;

        /// <summary>The slot holds a value (false — removed); the debugger skips empty slots.</summary>
        public bool HasValueAt(int index) => index >= 0 && index < _slots.Count && _slots[index].HasValue;

        public bool IsTagAt(int index) => index >= 0 && index < _slots.Count && _slots[index] is Slot<TagMark>;

        /// <summary>Boxed value — the debugger and the inspector only (allocates for value types).</summary>
        public object BoxedValueOf(int index) => index >= 0 && index < _slots.Count ? _slots[index].Boxed : null;

        public Type TypeOf(int index) => index >= 0 && index < _slots.Count ? _slots[index].ValueType : null;

        public void Clear()
        {
            _indices.Clear();
            _slots.Clear();
            Array.Clear(_slotById, 0, _slotById.Length);
            Layout++;
            Generation++;
        }

        private Slot<T> SlotAt<T>(BlackboardKey<T> key) =>
            key.Index >= 0 && key.Index < _slots.Count ? _slots[key.Index] as Slot<T> : null;

        private BlackboardKey<T> SlotFor<T>(string name, out bool created)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("A blackboard variable needs a name", nameof(name));

            if (_indices.TryGetValue(name, out int existing))
            {
                if (_slots[existing] is not Slot<T>)
                {
                    throw new InvalidOperationException(
                        $"Variable '{name}' is already declared as {_slots[existing].ValueType.Name}, not {typeof(T).Name}");
                }

                created = false;
                return new BlackboardKey<T>(existing);
            }

            _slots.Add(new Slot<T> { SlotName = name });
            int index = _slots.Count - 1;
            _indices.Add(name, index);
            int id = NameToId(name);
            if (id >= _slotById.Length)
                Array.Resize(ref _slotById, Math.Max(id + 1, _slotById.Length * 2));
            _slotById[id] = index + 1;
            Layout++;
            created = true;
            return new BlackboardKey<T>(index);
        }

        private void Write<T>(Slot<T> slot, int index, T value)
        {
            slot.Value = value;
            slot.Present = true;
            MarkChanged(slot, index);
        }

        private void MarkChanged(ISlot slot, int index)
        {
            slot.Bump();
            Changed?.Invoke(index);
        }
    }
}
