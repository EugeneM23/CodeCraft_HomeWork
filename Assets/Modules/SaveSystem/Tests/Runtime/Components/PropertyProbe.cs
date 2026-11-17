using Saving.Objects;
using UnityEngine;

namespace Saving.Tests
{
    /// <summary>[Save] on properties: an auto-property, one under its former name and one that cannot be written.</summary>
    public sealed class PropertyProbe : MonoBehaviour
    {
        [Save] public int Score { get; set; }
        [Save("_old")] public string Renamed { get; set; }
        [Save] public int GetOnly => Score * 2;
    }
}
