using Gameplay;
using UnityEngine;

namespace Game.Scripts.Player
{
    public class ShadowInstaller : Installer
    {
        [SerializeField] private Transform _transform;
        [SerializeField] private Rigidbody2D _rigidbody;
        [SerializeField] private CircleCollider2D _collider;
        [SerializeField] private LayerMask _groundLayer;

        public override void Install(DiContainer container)
        {
            container.BindSingle(_rigidbody);
            container.BindSingle(new CollisionComponent(_collider, _groundLayer));
            container.BindSingle(new RotationComponent(_transform));
            container.BindSingle(new Rotationcotroller());
        }
    }
}