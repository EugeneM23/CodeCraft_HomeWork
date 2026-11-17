using System;
using System.Collections.Generic;
using Saving.Objects;
using UnityEngine;

namespace Saving.Tests
{
    /// <summary>A script with [Save] fields of every kind the plugin promises to keep, and a setting that it must not.</summary>
    public class FieldsProbe : FieldsProbeBase
    {
        [Serializable]
        public sealed class Inner
        {
            public int A;
            [SerializeField] private int _b;

            public int C { get; set; }

            public int B
            {
                get => _b;
                set => _b = value;
            }
        }

        [SerializeField] private float _setting = 0.5f;
        [Save] private int _hp;
        [Save] public string Title;
        [Save("_start")] private Vector2 _startPosition;
        [Save] private readonly int _fixed = 3;
        [Save] public Inner Data = new();
        [Save] public List<int> Items = new();
        [Save] public Dictionary<string, int> Scores = new();

        public float Setting
        {
            get => _setting;
            set => _setting = value;
        }

        public int Hp
        {
            get => _hp;
            set => _hp = value;
        }

        public Vector2 StartPosition
        {
            get => _startPosition;
            set => _startPosition = value;
        }

        public int Fixed => _fixed;
    }
}
