using System;
using Saving.Objects;
using UnityEngine;

namespace Saving.Tests
{
    /// <summary>Save callbacks that throw: the layer must log them and go on.</summary>
    public sealed class SaveTestThrowingCallbacks : MonoBehaviour, ISaveCallbacks
    {
        public const string MESSAGE = "before save broke";

        public void OnBeforeSave() => throw new InvalidOperationException(MESSAGE);

        public void OnAfterLoad() => throw new InvalidOperationException("after load broke");
    }
}
