using System.Collections.Generic;

namespace Ticking
{
    /// <summary>
    /// Listeners of one phase, sorted by order and then by the order they came in. Safe to change while a pass walks
    /// it: a listener removed in the middle of the pass leaves a hole that the pass skips, an added one waits for the
    /// pass to end. The holes are swept and the waiting ones put in place when the outermost pass ends.
    /// </summary>
    internal sealed class ListenerList<T> where T : class
    {
        private struct Entry
        {
            public T Listener;
            public int Order;
        }

        private readonly List<Entry> _entries = new();
        private readonly List<Entry> _waiting = new();
        private int _passes;
        private bool _hasHoles;

        /// <summary>Listeners in the list, waiting ones included.</summary>
        public int Count { get; private set; }

        /// <summary>Slots a pass walks: <see cref="this[int]"/> is null for a hole.</summary>
        public int SlotCount => _entries.Count;

        public T this[int slot] => _entries[slot].Listener;

        public void Add(T listener, int order)
        {
            var entry = new Entry { Listener = listener, Order = order };
            if (_passes > 0)
                _waiting.Add(entry);
            else
                Insert(entry);

            Count++;
        }

        public void Remove(T listener)
        {
            for (int i = 0; i < _waiting.Count; i++)
            {
                if (ReferenceEquals(_waiting[i].Listener, listener))
                {
                    _waiting.RemoveAt(i);
                    Count--;
                    return;
                }
            }

            for (int i = 0; i < _entries.Count; i++)
            {
                if (!ReferenceEquals(_entries[i].Listener, listener))
                    continue;

                if (_passes > 0)
                {
                    _entries[i] = default;
                    _hasHoles = true;
                }
                else
                {
                    _entries.RemoveAt(i);
                }

                Count--;
                return;
            }
        }

        public void BeginPass() => _passes++;

        public void EndPass()
        {
            if (--_passes > 0)
                return;

            if (_hasHoles)
            {
                _entries.RemoveAll(static entry => entry.Listener == null);
                _hasHoles = false;
            }

            foreach (Entry entry in _waiting)
                Insert(entry);

            _waiting.Clear();
        }

        // After every listener of the same or lower order: equal orders keep the order they came in
        private void Insert(Entry entry)
        {
            int index = _entries.Count;
            while (index > 0 && _entries[index - 1].Order > entry.Order)
                index--;

            _entries.Insert(index, entry);
        }
    }
}
