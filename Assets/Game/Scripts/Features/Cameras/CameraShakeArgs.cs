using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Game
{
    [Serializable]
    public class CameraShakeArgs
    {
        [field: SerializeField] public float ShakeDuration { get; private set; }
        [field: SerializeField] public float ShakeStrength { get; private set; }
    }
}