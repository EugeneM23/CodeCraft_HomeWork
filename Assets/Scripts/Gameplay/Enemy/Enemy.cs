using UnityEngine;

namespace Gameplay
{
    public sealed class Enemy : Character
    {
        private EnemyPool _pool;
        private Transform _attackTarget;

        protected void OnEnable()
        {
            _currentHealth = _health;
            GetComponent<EnemyAI>().OnShoot += Shoot;
        }

        private void OnDisable() =>
            GetComponent<EnemyAI>().OnShoot -= Shoot;

        public override void Move(Vector3 direction) =>
            transform.position = Vector3.MoveTowards(transform.position, direction, _speed * Time.deltaTime);

        public override void Shoot() =>
            _gun.Shoot(_attackTarget.position - transform.position);

        protected override void OnDeath() =>
            _pool.Return(this);

        public void SetTarget(Transform target) => _attackTarget = target;

        public void SetPull(EnemyPool pool) => _pool = pool;
    }
}