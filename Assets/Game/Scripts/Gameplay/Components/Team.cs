using SampleGame.Common;
using SaveLoadSystem;
using UnityEngine;

namespace SampleGame.Gameplay
{
    //Can be extended
    public sealed class Team : MonoBehaviour
    {
        ///Variable
        [field: SerializeField]
        [SaveField]
        public TeamType Type { get; set; }
    }
}