using UnityEngine;

namespace Game
{
    public abstract class BuffConfig : ScriptableObject
    {
        [field: SerializeField] public string Name { get; private set; }
        [field: SerializeField] public Sprite Icon { get; private set; }
        [field: SerializeField] public float Duration { get; private set; }

        public abstract BuffBase CreateBuff(); 
    }
}