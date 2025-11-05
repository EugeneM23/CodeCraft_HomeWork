using Modules.PlayerController;
using UnityEngine;

namespace Gameplay.Controllers.AttackAction
{
    public class SpawnHitEffectAttackAction : AttackComponent.IHitAction
    {
        private readonly Transform _prefab;

        public SpawnHitEffectAttackAction(Transform prefab)
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