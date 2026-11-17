using UnityEngine;

namespace Saving.Tests
{
    /// <summary>A component saved by its own serializer (ProbeSerializer), not by fields.</summary>
    public sealed class SerializerProbe : MonoBehaviour
    {
        public float Speed;
        public string Mode;
    }
}
