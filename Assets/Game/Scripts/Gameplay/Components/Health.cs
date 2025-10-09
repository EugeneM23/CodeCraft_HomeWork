using SaveLoadSystem;
using UnityEngine;

namespace SampleGame.Gameplay
{
    //Can be extended
    public sealed class Health : MonoBehaviour
    {
        ///Variable
        [field: SerializeField]
        [SaveField]
        public int Current { get; set; } = 50;

        ///Const
        [field: SerializeField]
        [SaveField]
        public int Max { get; private set; } = 100;
    }
}