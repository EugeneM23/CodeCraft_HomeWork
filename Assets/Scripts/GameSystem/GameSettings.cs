using System;
using UnityEngine;

namespace Game
{
    [Serializable]
    public class GameSettings
    {
        [field: SerializeField] public int MaxLevel { get; private set; }
        [field: SerializeField] public int Acceleration { get; private set; }
        [field: SerializeField] public int StartSpeed { get; private set; }
    }
}