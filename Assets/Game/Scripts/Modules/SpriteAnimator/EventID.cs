using System;
using Sirenix.Serialization;

namespace Gameplay
{
    [Serializable]
    public enum EventID
    {
        Attack_01,
        Attack_02,
        Step,
        PushAbilitySide,
        PushAbilityUP,
        None
    }
}