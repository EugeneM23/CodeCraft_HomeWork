using System.Collections.Generic;
using Saving.Objects;
using UnityEngine;

namespace Saving.Tests
{
    /// <summary>[Save] references of every kind: a Saveable, a component, a child, a prefab, an asset, a list.</summary>
    public sealed class SaveTestLinks : MonoBehaviour
    {
        [Save] public Saveable Target;
        [Save] public SaveTestCounter Counter;
        [Save] public Transform Handle;
        [Save] public GameObject Prefab;
        [Save] public Sprite Icon;
        [Save] public List<Saveable> Many = new();
    }
}
