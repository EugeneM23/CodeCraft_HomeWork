using Modules.PlayerController;
using UnityEngine;

namespace Gameplay.Controllers.AttackAction
{
    public class SpawnHitEffectAction : AttackComponent.IEntityHitAction
    {
        private readonly Transform _prefab;

        public SpawnHitEffectAction(Transform prefab)
        {
            _prefab = prefab;
        }

        public void Invoke(RaycastHit2D hit, Entity entity)
        {
            var transform = SceneContext.Instance.Container.InstantiatePrefab(_prefab, entity.transform.position,
                Quaternion.identity);
        }
    }
}