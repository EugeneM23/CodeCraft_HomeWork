using UnityEngine;

namespace Saving.Tests
{
    /// <summary>A script with nothing to save: no [Save] fields and no serializer.</summary>
    public sealed class PlainProbe : MonoBehaviour
    {
        public int Setting;
    }
}
