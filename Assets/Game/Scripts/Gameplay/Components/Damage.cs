using System;
using SaveLoadSystem;
using UnityEngine;

namespace SampleGame.Gameplay
{
    //Can be extended
    public sealed class Damage : MonoBehaviour
    {
        ///Const
        [field: SerializeField]
        [SaveField]
        public int Value { get; private set; } = 10;
    }
}