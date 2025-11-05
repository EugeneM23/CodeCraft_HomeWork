using Gameplay.Controllers;
using Modules.PlayerController;
using UnityEngine;

namespace Gameplay
{
    public class SpawnDashEffectAction : DashComponent.IAction
    {
        [Inject] private CharacterController2D _controller;
        private readonly Transform _prefab;

        public SpawnDashEffectAction(Transform prefab) => _prefab = prefab;

        public void Invoke()
        {
            if (!_controller.IsGrounded) return;

            float offset = _controller.transform.position.y - _controller.Collider.size.y / 2;
            Vector3 position = _controller.transform.position;
            position.y = offset;

            Transform prefab =
                SceneContext.Instance.Container.InstantiatePrefab(_prefab, position, Quaternion.identity);
            var spriteRenderer = prefab.GetComponent<SpriteRenderer>();

            if (_controller.Velocity.x < 0) spriteRenderer.flipX = true;
        }
    }
}