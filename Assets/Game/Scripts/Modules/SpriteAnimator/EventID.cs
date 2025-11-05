using System;
using Sirenix.Serialization;

namespace Gameplay
{
    [Serializable]
    public enum EventID
    {
        None,
        CastDamage,
        ThrowItem,
    }
}