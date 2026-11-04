using Atomic.Entities;
using UnityEngine;

namespace Game.Sandbox
{
    /// <summary>
    /// Wires a trigger volume up as a spawn button: running into it puts one enemy at the spawn
    /// point.
    /// </summary>
    public sealed class SpawnButtonInstaller : MonoEntityInstaller
    {
        [SerializeField] private TriggerEventReceiver _trigger;
        [SerializeField] private MonoEntity _enemyPrefab;
        [SerializeField] private Transform _spawnPoint;
        [SerializeField] private float _cooldown = 1f;

        public override void Install(IEntity entity)
        {
            entity.AddGameObject(this.gameObject);
            entity.AddTransform(this.transform);

            // Checked before anything is added: AddValue throws on null and an exception from an
            // installer aborts the rest of Install, which is how the zombie lost its health and AI.
            if (_trigger == null || _enemyPrefab == null || _spawnPoint == null)
            {
                Debug.LogWarning($"{nameof(SpawnButtonInstaller)} on '{this.name}' is not wired.", this);
                return;
            }

            entity.AddBehaviour(new SpawnButtonBehaviour(_trigger, _enemyPrefab, _spawnPoint, _cooldown));
        }
    }
}
