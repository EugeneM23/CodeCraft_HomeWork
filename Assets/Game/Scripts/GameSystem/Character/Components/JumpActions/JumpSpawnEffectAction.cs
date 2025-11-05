using Modules.PlayerController;
using UnityEngine;

namespace Gameplay
{
    public class SpawnJumpEffectAction : JumpComponent.IAction
    {
        private readonly Transform _prefab;

        [Inject] private CharacterController2D _controller;

        public SpawnJumpEffectAction(Transform prefab)
        {
            _prefab = prefab;
        }

        public void Invoke()
        {
            float offset = _controller.transform.position.y - _controller.Collider.size.y / 2;
            Vector3 position = _controller.transform.position;
            position.y = offset;

            SceneContext.Instance.Container.InstantiatePrefab(_prefab, position,
                Quaternion.identity);
        }
    }
}