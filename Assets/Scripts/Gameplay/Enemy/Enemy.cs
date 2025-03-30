using UnityEngine;

namespace Gameplay
{
    [RequireComponent(typeof(EnemyAI))]
    public sealed class Enemy : Character
    {
        [SerializeField] private EnemyAI _enemyAI;

        private Transform _attackTarget;
        private EnemyPool _pool;

        protected void OnEnable() => _enemyAI.OnShoot += Shoot;

        private void OnDisable() => _enemyAI.OnShoot -= Shoot;

        public override void Shoot() => _weapon.Shoot(_attackTarget.position - transform.position);

        protected override void OnDeath() => _pool.Return(this);

        public void SetTarget(Transform target) => _attackTarget = target;

        public void SetEnemyPull(EnemyPool pool) => _pool = pool;

    }
}