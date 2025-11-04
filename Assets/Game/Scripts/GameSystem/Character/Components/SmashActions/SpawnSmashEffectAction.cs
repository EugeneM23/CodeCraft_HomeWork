using Gameplay.Controllers;
using Modules.PlayerController;
using UnityEngine;

namespace Gameplay
{
    public class SpawnSmashEffectAction : SmashComponent.IAction, ITickable
    {
        [Inject] private CharacterController2D _controller;

        private readonly Transform _prefab;
        private bool _effectSpawned = true;

        public SpawnSmashEffectAction(Transform prefab) => _prefab = prefab;

        public void Invoke()
        {
            _effectSpawned = false;
        }

        public void Tick()
        {
            if (!_effectSpawned && _controller.IsGrounded)
            {
                float offset = _controller.transform.position.y - _controller.Collider.size.y;
                Vector3 position = _controller.transform.position;
                position.y = offset;

                SceneContext.Instance.Container.InstantiatePrefab(_prefab, position, Quaternion.identity);
                _effectSpawned = true;
            }
        }
    }
}