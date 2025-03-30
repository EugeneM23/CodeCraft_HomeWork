using UnityEngine;

namespace Gameplay
{
    public abstract class Character : MonoBehaviour, IDamageable
    {
        [SerializeField] protected int _health;
        [SerializeField] protected float _speed;
        [SerializeField] protected Weapon _gun;
        [SerializeField] private Rigidbody2D _rigidbody;

        protected int _currentHealth;

        public void SetBulletPoolToGun(BulletPool pool) =>
            _gun.SetPool(pool);

        public virtual void Shoot() =>
            _gun.Shoot(Vector3.up);

        public virtual void Move(Vector3 direction)
        {
            transform.Translate(direction * _speed * Time.deltaTime);
        }

        public virtual void TakeDamage(int damage)
        {
            _health -= damage;
            if (_health <= 0)
                OnDeath();
        }

        protected abstract void OnDeath();
    }
}