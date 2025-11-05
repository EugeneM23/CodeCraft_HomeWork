using Modules.PlayerController;
using UnityEngine;

namespace Gameplay
{
    public class JumpVFXController : IInitializeble, IDisposable
    {
        private readonly Transform _prefab;

        [Inject] private CharacterController2D _controller;
        [Inject] private Character _character;

        public JumpVFXController(Transform prefab)
        {
            _prefab = prefab;
        }

        private void SpawnEffect()
        {
            float offset = _controller.transform.position.y - _controller.Collider.size.y / 2;
            Vector3 position = _controller.transform.position;
            position.y = offset;

            SceneContext.Instance.Container.InstantiatePrefab(_prefab, position,
                Quaternion.identity);
        }

        public void Initialize() => _character.OnJump += SpawnEffect;

        public void Dispose() => _character.OnJump -= SpawnEffect;
    }
}