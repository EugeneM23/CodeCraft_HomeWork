using UnityEngine;

namespace Gameplay
{
    public class PlayerInstaller : Installer
    {
        [Header("Character Settings")] [SerializeField]
        private float _moveSpeed = 5f;

        [SerializeField] private float _jumpForce = 5f;
        [SerializeField] private int _health = 100;
        [SerializeField] private int _gravityScale = 20;

        [Header("Components")] [SerializeField]
        private Transform _transform;

        [SerializeField] private TakeDamageProxy _damageProxy;
        [SerializeField] private Rigidbody2D _rigidbody;
        [SerializeField] private CircleCollider2D _collider;

        [Header("Environment Settings")] [SerializeField]
        private LayerMask _groundLayer;

        [SerializeField] private SpriteRenderer _material;

        public override void Install(DiContainer container)
        {
            /*
            container.Bind("assdasdasd");
            container.Bind("1111111111");
            container.Bind("22222222222");
            container.Bind("3333333333");
            container.Bind("4444444444");
            */
            
            container.BindSingle(new ShadowFadeComponent(_material, _rigidbody));
            container.BindSingle(new GroundSyncComponent());
            container.BindSingle(_rigidbody);
            container.BindSingle(new Player());
            container.BindSingle(new AttackComponent());
            container.BindSingle(new AttackCooldown());
            container.BindSingle(new CollisionComponent(_collider, _groundLayer));
            container.BindSingle(new GravityScaleComponent(_rigidbody, _gravityScale));
            container.BindSingle(_collider);
            container.BindSingle(new JumpController());
            container.BindSingle(new JumpComponent(_jumpForce, _rigidbody));

            container.BindSingle(new MoveController());
            container.BindSingle(new RotationComponent(_transform));
            container.BindSingle<IMovabele>(new MoveComponent(_rigidbody, _moveSpeed));
            container.BindSingle(new DeathObserver());
            container.BindSingle(new HealthComponent(_health));
            container.BindSingle(_transform);
        }
    }
}