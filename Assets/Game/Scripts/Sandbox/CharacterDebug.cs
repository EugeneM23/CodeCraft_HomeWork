using Atomic.Entities;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Sandbox
{
    public class CharacterDebug : MonoBehaviour
    {
        [SerializeField] private MonoEntity _character;

        [Button]
        public bool Apply(BuffConfig buff) => BuffUseCase.Apply(_character, buff.CreateBuff());

        [Button]
        public bool Discard(BuffConfig buff) => BuffUseCase.Discard(_character, buff.Name);
    }  
}