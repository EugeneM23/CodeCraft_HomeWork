using System;
using System.Collections.Generic;
using Saving.Objects;
using UnityEngine;

namespace Saving.Tests
{
    /// <summary>
    /// Stands for a game object whose state lives in plain classes (a DI container): it hands them over to the save.
    /// </summary>
    public sealed class StateProviderProbe : MonoBehaviour, ISaveStateProvider
    {
        /// <summary>Marked members: only they are saved.</summary>
        public sealed class Health : ISaveCallbacks
        {
            [Save] private int _current = 100;
            private int _shown = 100; // what a view shows: not saved, brought up to date in OnAfterLoad

            public int Current
            {
                get => _current;
                set => _current = value;
            }

            public int Shown => _shown;
            public int BeforeSaves { get; private set; }

            public void OnBeforeSave() => BeforeSaves++;
            public void OnAfterLoad() => _shown = _current;
        }

        /// <summary>No marks: public values and the simple private ones; what it was given is left alone.</summary>
        public sealed class Clip
        {
            public int Capacity = 30;
            private int _bullets = 30;
            private readonly int _given = 7;
            private List<int> _history = new();
            private object _service = new();
            [NonSerialized] private int _scratch;

            public int Bullets
            {
                get => _bullets;
                set => _bullets = value;
            }

            public float Heat { get; private set; }
            public int Given => _given;
            public List<int> History => _history;
            public object Service => _service;

            public int Scratch => _scratch;

            public void SetScratch(int value) => _scratch = value;

            public void Warm(float heat) => Heat = heat;
        }

        public readonly Health Life = new();
        public readonly Clip First = new();
        public readonly Clip Second = new();

        /// <summary>Tests switch it on to see a provider that throws.</summary>
        public bool Throws;

        public void CollectState(List<object> state)
        {
            if (Throws)
                throw new InvalidOperationException("the container is gone");

            state.Add(Life);
            state.Add(First);
            state.Add(Second);
            state.Add(null);
            state.Add(transform); // a Unity object: components are saved as components
        }
    }
}
