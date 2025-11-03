using Modules.PlayerController;
using UnityEngine;

namespace Gameplay
{
    public class SpawnDeathEffectAction : CharacterDeathObserver.IAction
    {
        private Transform _prefab;
        private float _force;

        public SpawnDeathEffectAction(Transform prefab, float force = 15f)
        {
            _prefab = prefab;
            _force = force;
        }

        public void Invoke(CharacterController2D character)
        {
            Transform instance = SceneContext.Instance.Container.InstantiatePrefab(
                _prefab,
                character.transform.position,
                Quaternion.identity
            );

            Rigidbody2D[] components = instance.GetComponentsInChildren<Rigidbody2D>();

            foreach (var rb in components)
            {
                Vector2 randomDir = Random.insideUnitCircle.normalized;

                rb.AddForce(randomDir * _force, ForceMode2D.Impulse);
            }
        }
    }
}