using UnityEngine;

namespace Gameplay.Controllers.AttackAction
{
    public class SpawnHitEffectHealthAction : HealthComponent.IAction
    {
        [Inject] private readonly Entity entity;
        private readonly Transform _prefab;

        public SpawnHitEffectHealthAction(Transform prefab)
        {
            _prefab = prefab;
        }

        public void Invoke()
        {
            SceneContext.Instance.Container.InstantiatePrefab(_prefab, entity.transform.position,
                Quaternion.identity);
        }
    }
}