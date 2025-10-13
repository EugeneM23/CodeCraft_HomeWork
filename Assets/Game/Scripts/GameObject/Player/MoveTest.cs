using Gameplay;
using UnityEngine;

namespace Game.Scripts.Player
{
    public class MoveTest : MonoBehaviour
    {
        private MoveComponent _moveComponent;

        public void Construct(MoveComponent moveComponent)
        {
            _moveComponent = moveComponent;
        }
    }
}