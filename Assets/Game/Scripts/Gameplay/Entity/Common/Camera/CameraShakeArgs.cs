using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Game
{
    [Serializable]
    public class CameraShakeArgs
    {
        [FormerlySerializedAs("_shakeDuration")] [SerializeField] public float ShakeDuration;
        [FormerlySerializedAs("_shakeStrength")] [SerializeField]
        public float ShakeStrength;
    }
}