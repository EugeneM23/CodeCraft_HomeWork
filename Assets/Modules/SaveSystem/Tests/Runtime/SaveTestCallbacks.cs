using Saving.Objects;
using UnityEngine;

namespace Saving.Tests
{
    /// <summary>Counts the save callbacks it gets.</summary>
    public sealed class SaveTestCallbacks : MonoBehaviour, ISaveCallbacks
    {
        public int BeforeSave { get; private set; }
        public int AfterLoad { get; private set; }

        public void OnBeforeSave() => BeforeSave++;
        public void OnAfterLoad() => AfterLoad++;
    }
}
