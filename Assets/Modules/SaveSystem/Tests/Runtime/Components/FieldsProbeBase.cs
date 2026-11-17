using Saving.Objects;
using UnityEngine;

namespace Saving.Tests
{
    /// <summary>Base of FieldsProbe: an inherited [Save] field.</summary>
    public abstract class FieldsProbeBase : MonoBehaviour
    {
        [Save] protected int _level;

        public int Level
        {
            get => _level;
            set => _level = value;
        }
    }
}
