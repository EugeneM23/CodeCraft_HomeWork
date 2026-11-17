using System.Collections.Generic;
using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// Something said or heard (handbook §10): an id, who sent it, where, a value, and what it is about (the target an
    /// ally shouts about). Not a string in a bag: hearing and shouting need the position.
    /// </summary>
    public readonly struct Signal
    {
        public readonly string Id;
        public readonly Transform Sender;
        public readonly Vector3 Position;
        public readonly float Value;
        public readonly Transform Subject;

        public Signal(string id, Vector3 position, Transform sender = null, Transform subject = null, float value = 0f)
        {
            Id = id;
            Position = position;
            Sender = sender;
            Subject = subject;
            Value = value;
        }
    }

    /// <summary>Who hears a signal: within a radius of its position, and (optionally) only one faction.</summary>
    public readonly struct SignalArea
    {
        public readonly float Radius;
        public readonly bool AnyFaction;
        public readonly int Faction;

        private SignalArea(float radius, bool anyFaction, int faction)
        {
            Radius = radius;
            AnyFaction = anyFaction;
            Faction = faction;
        }

        public static SignalArea Within(float radius) => new(radius, true, 0);

        public static SignalArea FactionWithin(int faction, float radius) => new(radius, false, faction);

        public bool Reaches(ISignalListener listener, in Signal signal) =>
            (AnyFaction || listener.Faction == Faction) &&
            (Radius <= 0f || (listener.ListenPosition - signal.Position).sqrMagnitude <= Radius * Radius);
    }

    public interface ISignalListener
    {
        /// <summary>The listener's entity: a signal sent by this entity is not delivered back to it.</summary>
        Transform Root { get; }

        Vector3 ListenPosition { get; }

        int Faction { get; }

        void Hear(in Signal signal);
    }

    /// <summary>
    /// The minimal bus: broadcast with an area, subscribe and unsubscribe. A null bus is fine — the agent just never
    /// hears anything. The areas "squad" and "everyone", the modes Restart / Once / Queue and filters come with their
    /// consumer (the coordinator, "on demand").
    /// </summary>
    public interface ISignalBus
    {
        void Subscribe(ISignalListener listener);

        void Unsubscribe(ISignalListener listener);

        /// <summary>Delivers the signal to every listener the area reaches, except its sender; returns how many heard it.</summary>
        int Broadcast(in Signal signal, SignalArea area);
    }

    /// <summary>
    /// The bus as a plain object (one per world or per game: the game registers it in its services). Delivery goes over
    /// a copy of the listeners, so a listener may subscribe or unsubscribe anyone, itself included, while hearing — and
    /// broadcast itself (a shout passed on): that delivery goes over a copy of its own, and the first one still reaches
    /// everyone after it.
    /// </summary>
    public sealed class SignalBus : ISignalBus
    {
        private readonly List<ISignalListener> _listeners = new();

        // One copy of the listeners per delivery in progress (a broadcast from inside a Hear is one deeper): made the
        // first time that depth is reached, reused after
        private readonly List<List<ISignalListener>> _deliveries = new();
        private int _depth;

        public int Count => _listeners.Count;

        public void Subscribe(ISignalListener listener)
        {
            if (listener != null && !_listeners.Contains(listener))
                _listeners.Add(listener);
        }

        public void Unsubscribe(ISignalListener listener) => _listeners.Remove(listener);

        public int Broadcast(in Signal signal, SignalArea area)
        {
            if (_depth == _deliveries.Count)
                _deliveries.Add(new List<ISignalListener>());

            List<ISignalListener> delivering = _deliveries[_depth];
            delivering.Clear();
            delivering.AddRange(_listeners);
            _depth++;
            int heard = 0;
            try
            {
                for (int i = 0; i < delivering.Count; i++)
                {
                    ISignalListener listener = delivering[i];
                    if (listener == null || !_listeners.Contains(listener) ||
                        (signal.Sender != null && listener.Root == signal.Sender) || !area.Reaches(listener, signal))
                        continue;

                    listener.Hear(signal);
                    heard++;
                }
            }
            finally
            {
                _depth--;
                delivering.Clear();
            }

            return heard;
        }
    }
}
