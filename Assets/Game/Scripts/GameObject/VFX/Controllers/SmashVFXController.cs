using Modules.PlayerController;
using UnityEngine;

namespace Gameplay
{
    public class SmashVFXController : IInitializeble
    {
        [Inject] private CharacterController2D _controller;
        private readonly Transform _prefab;

        public SmashVFXController(Transform prefab) => _prefab = prefab;

        private void SpawnEffect(Vector2 veloscity)
        {
            if (veloscity.y > -50) return;

            float offset = _controller.transform.position.y - (_controller.Collider.size.y);
            Vector3 position = _controller.transform.position;
            position.y = offset;

            SceneContext.Instance.Container.InstantiatePrefab(_prefab, position,
                Quaternion.identity);
        }

        public void Initialize()
        {
            _controller.OnGrounded += SpawnEffect;
        }
    }
}