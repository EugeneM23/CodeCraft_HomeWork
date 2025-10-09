using SaveLoadSystem;
using UnityEngine;

namespace SampleGame.Gameplay
{
    public class TransformSerializer : GameSerializer<Transform, SerializedTransform>
    {
        protected override SerializedTransform Serialize(Transform transform) => transform;

        protected override void Deserialize(Transform service, SerializedTransform data) => data.ApplyTo(service);
    }
}