using Atomic.Entities;
using Game.Gameplay;
using UnityEngine;

namespace Game
{
    public class TestController : MonoBehaviour
    {
        [SerializeField] private SceneEntity _character;

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                _character.DelBehaviour<LookAtBehaviour>();
            }

            if (Input.GetKeyDown(KeyCode.U))
            {
                _character.AddBehaviour<LookAtBehaviour>();
            }
        }
    }
}