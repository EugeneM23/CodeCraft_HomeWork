using Game.Scripts.GameObject.Player;
using Gameplay;
using UnityEngine;

namespace Game.Scripts.GameObject.Enemy
{
    public class TrapInstaller : Installer
    {
        [SerializeField] private Rigidbody2D _rigidbody;

        public override void Install(DiContainer container)
        {
            container.BindSingle(new ImpulseComponent(_rigidbody));
            container.BindSingle(new GravityScaleComponent(_rigidbody, 10));
        }
    }
}