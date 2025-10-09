using SampleGame.Common;
using SaveLoadSystem;
using UnityEngine;

namespace SampleGame.Gameplay
{
    //Can be extended
    public sealed class ResourceBag : MonoBehaviour
    {
        ///Variable
        [field: SerializeField]
        [SaveField]
        public ResourceType Type { get; set; }
        
        ///Variable
        [field: SerializeField]
        [SaveField]

        public int Current { get; set; }
        
        ///Const
        [field: SerializeField]
        [SaveField]

        public int Capacity { get; set; }
    }
}