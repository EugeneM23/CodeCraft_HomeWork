using System;
using System.Collections.Generic;
using UnityEngine;

namespace Saving.Tests
{
    /// <summary>
    /// Serialized fields whose values hide Unity references inside (a wave with its prefab, a map of objects) next to
    /// plain data: All Fields must take the plain data only, or a load would wipe the wiring.
    /// </summary>
    public sealed class NestedProbe : MonoBehaviour
    {
        [Serializable]
        public sealed class Wave
        {
            public GameObject Prefab;
            public int Count;
        }

        [Serializable]
        public sealed class Stats
        {
            public int Kills;
            public List<int> Scores = new();
        }

        [Serializable]
        public struct Deep
        {
            public Wave Inner;
        }

        public Wave[] Waves = new Wave[0];
        public Dictionary<string, GameObject> Map = new();
        public Deep Nested;
        public Stats Plain = new();
    }
}
