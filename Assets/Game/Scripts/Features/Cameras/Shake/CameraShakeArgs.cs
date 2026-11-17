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

        public CameraShakeArgs()
        {
        }

        /// <summary>For a shake worked out in code (an air bomb's, fading with distance).</summary>
        public CameraShakeArgs(float duration, float strength)
        {
            ShakeDuration = duration;
            ShakeStrength = strength;
        }
    }
}