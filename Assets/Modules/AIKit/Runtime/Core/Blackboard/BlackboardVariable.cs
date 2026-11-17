namespace AIKit
{
    /// <summary>
    /// A node's handle on a blackboard variable it knows by name: the name becomes a process-wide id once
    /// (Blackboard.NameToId), and every read after that finds the slot of whatever blackboard it is asked for by an
    /// array index — so one shared node serves any number of agents without hashing. A name that does not exist yet is
    /// not declared (reading stays pure). Keep it in a field — a copy (a property getter) loses the cached id.
    /// </summary>
    public struct BlackboardVariable<T>
    {
        private string _name;
        private int _id;

        public T Get(Blackboard board, string name, T fallback = default)
        {
            if (board == null)
                return fallback;

            if (Resolve(board, name, out BlackboardKey<T> key))
                return board.Get(key, fallback);

            // A number of another number type (an int variable in a float pin): read as this type
            return Numbers.Is<T>() && !string.IsNullOrEmpty(name) &&
                   board.TryGetNumberAt(board.IndexOfId(_id), out double number)
                ? Numbers.From<T>(number)
                : fallback;
        }

        public bool TryGet(Blackboard board, string name, out T value)
        {
            if (board != null && Resolve(board, name, out BlackboardKey<T> key))
                return board.TryGet(key, out value);

            if (board != null && Numbers.Is<T>() && !string.IsNullOrEmpty(name) &&
                board.TryGetNumberAt(board.IndexOfId(_id), out double number))
            {
                value = Numbers.From<T>(number);
                return true;
            }

            value = default;
            return false;
        }

        public int Version(Blackboard board, string name) =>
            board != null && Resolve(board, name, out BlackboardKey<T> key) ? board.VersionOf(key.Index) : 0;

        public void Set(Blackboard board, string name, T value)
        {
            if (board == null || string.IsNullOrEmpty(name))
                return;

            if (Resolve(board, name, out BlackboardKey<T> key))
                board.Set(key, value);
            else if (!Numbers.Is<T>() || !board.SetNumberAt(board.IndexOfId(_id), Numbers.To(value)))
                board.Set(name, value);             // creates the slot; the next read finds it by id
        }

        private bool Resolve(Blackboard board, string name, out BlackboardKey<T> key)
        {
            if (string.IsNullOrEmpty(name))
            {
                key = BlackboardKey<T>.None;
                return false;
            }

            if (!ReferenceEquals(name, _name))
            {
                _name = name;
                _id = Blackboard.NameToId(name);
            }

            int index = board.IndexOfId(_id);
            if (index >= 0 && board.IsOfType<T>(index))
            {
                key = new BlackboardKey<T>(index);
                return true;
            }

            key = BlackboardKey<T>.None;
            return false;
        }
    }
}
