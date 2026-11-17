using Saving.Objects;
using UnityEngine;

namespace Saving.Tests
{
    /// <summary>A [Save] value that the object's own Start resets, as game code often does.</summary>
    public sealed class SaveTestCounter : MonoBehaviour
    {
        [SerializeField] private int _initialValue = 1;
        [Save] public int Value;

        public int Starts { get; private set; }

        private void Start()
        {
            Value = _initialValue;
            Starts++;
        }
    }
}
