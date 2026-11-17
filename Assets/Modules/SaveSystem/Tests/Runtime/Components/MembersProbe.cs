using System;
using System.Collections.Generic;
using UnityEngine;

namespace Saving.Tests
{
    /// <summary>
    /// A script without [Save]: settings, runtime state, wiring and things no save can hold. All Fields takes the
    /// serialized values, Selected may take any field or property.
    /// </summary>
    public sealed class MembersProbe : MonoBehaviour
    {
        public int Health = 10;
        [SerializeField] private float _speed = 2f;
        public List<int> Ids = new();
        [NonSerialized] public int Skipped;
        private float _timer;
        public Transform Target;
        [SerializeField] private Rigidbody2D _body;
        public Action Callback;

        public int Prop { get; set; }
        public bool Unlocked { get; private set; }
        public int ReadOnlyProp => Health;

        public void Unlock() => Unlocked = true;

        public float Speed
        {
            get => _speed;
            set => _speed = value;
        }

        public float Timer
        {
            get => _timer;
            set => _timer = value;
        }

        public Rigidbody2D Body
        {
            get => _body;
            set => _body = value;
        }
    }
}
