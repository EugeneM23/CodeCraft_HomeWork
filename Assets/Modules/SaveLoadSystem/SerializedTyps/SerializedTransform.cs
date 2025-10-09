using System;
using SaveLoadSystem;
using UnityEngine;

namespace SaveLoadSystem
{
    [Serializable]
    public struct SerializedTransform
    {
        public SerializedVector3 position;
        public SerializedVector3 rotation;
        public SerializedVector3 scale;

        public SerializedTransform(Transform transform)
        {
            position = transform.localPosition;
            rotation = transform.localRotation;
            scale = transform.localScale;
        }

        public void ApplyTo(Transform transform)
        {
            transform.localPosition = position;
            transform.localRotation = rotation;
            transform.localScale = scale;
        }

        public static implicit operator SerializedTransform(Transform transform)
        {
            return new SerializedTransform(transform);
        }
    }
}