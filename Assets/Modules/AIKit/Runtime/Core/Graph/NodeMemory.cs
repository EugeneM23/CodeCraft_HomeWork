using System;
using System.Collections.Generic;

namespace AIKit
{
    /// <summary>
    /// What one agent remembers about the nodes it runs: time in a state, the step of a sequence, the waypoint of a
    /// patrol. A graph is a shared description (one graph serves any number of agents); everything that changes while
    /// it runs lives here, on the agent's context, never in the node. Nodes linked into a graph get a slot: their
    /// memory is an array cell of that graph's space (no hashing on the hot path). A node outside any linked graph (a
    /// task ticked by hand, a role subgraph) is remembered by reference instead — still per agent, only slower.
    /// </summary>
    public sealed class NodeMemory
    {
        private readonly Dictionary<BehaviorGraph, object[]> _spaces = new();
        private readonly Dictionary<object, object> _loose = new();
        private BehaviorGraph _lastSpace;
        private object[] _lastSlots;

        /// <summary>Memories held (slot cells in use plus loose ones): the debugger and the tests read it.</summary>
        public int Count
        {
            get
            {
                int count = _loose.Count;
                foreach (object[] slots in _spaces.Values)
                {
                    for (int i = 0; i < slots.Length; i++)
                    {
                        if (slots[i] != null)
                            count++;
                    }
                }

                return count;
            }
        }

        /// <summary>The memory of an element of a graph (made on first ask). Space and slot come from the graph's Link.</summary>
        internal T Of<T>(BehaviorGraph space, int slot, object key) where T : class, new()
        {
            if (space != null && slot >= 0)
            {
                object[] slots = SlotsOf(space, slot);
                if (slots[slot] is T kept)
                    return kept;

                var made = new T();
                slots[slot] = made;
                return made;
            }

            if (_loose.TryGetValue(key, out object found) && found is T loose)
                return loose;

            var fresh = new T();
            _loose[key] = fresh;
            return fresh;
        }

        /// <summary>Forgets everything (a new life of the agent: pooled and spawned again).</summary>
        public void Clear()
        {
            _spaces.Clear();
            _loose.Clear();
            _lastSpace = null;
            _lastSlots = null;
        }

        private object[] SlotsOf(BehaviorGraph space, int slot)
        {
            if (!ReferenceEquals(space, _lastSpace))
            {
                if (!_spaces.TryGetValue(space, out object[] slots))
                {
                    slots = new object[Math.Max(space.SlotCount, slot + 1)];
                    _spaces[space] = slots;
                }

                _lastSpace = space;
                _lastSlots = slots;
            }

            if (slot >= _lastSlots.Length)
            {
                Array.Resize(ref _lastSlots, Math.Max(space.SlotCount, slot + 1));
                _spaces[space] = _lastSlots;
            }

            return _lastSlots;
        }
    }
}
