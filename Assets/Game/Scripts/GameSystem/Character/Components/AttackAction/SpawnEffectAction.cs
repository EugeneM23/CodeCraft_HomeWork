using Modules.PlayerController;
using UnityEngine;

namespace Gameplay.Controllers.AttackAction
{
    public class SpawnEffectAction : AttackComponent.IAction
    {
        [Inject] private readonly CharacterController2D _character;
        private readonly Transform _prefab;

        public SpawnEffectAction(Transform prefab)
        {
            _prefab = prefab;
        }

        public void Invoke()
        {
            /*var transform = SceneContext.Instance.Container.InstantiatePrefab(_prefab, _character.transform.position,
                Quaternion.identity);*/
        }
    }
}