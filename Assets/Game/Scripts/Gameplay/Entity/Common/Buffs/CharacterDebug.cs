using Atomic.Entities;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Gameplay
{
    public class CharacterDebug : MonoBehaviour
    {
        [SerializeField] private SceneEntity _character;

        [Button]
        public bool Apply(BaseBuff buff) => BuffUseCase.Apply(_character, buff);

        [Button]
        public bool Discard(BaseBuff buff) => BuffUseCase.Discard(_character, buff);
    }  
}