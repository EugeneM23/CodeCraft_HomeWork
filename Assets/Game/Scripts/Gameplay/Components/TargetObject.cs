using Modules.Entities;
using SaveLoadSystem;
using UnityEngine;

namespace SampleGame.Gameplay
{
    //Can be extended
    public sealed class TargetObject : MonoBehaviour
    {
        ///Variable
        [field: SerializeField]
        public Entity Value { get; set; }
    }
}