using SaveLoadSystem;
using UnityEngine;

namespace SampleGame.Gameplay
{
    //Can be extended
    public sealed class Countdown : MonoBehaviour
    {
        ///Variable
        [field: SerializeField]
        [SaveField]
        public float Current { get; set; }

        ///Const
        [field: SerializeField]
        [SaveField]
        public float Duration { get; private set; }
    }
}