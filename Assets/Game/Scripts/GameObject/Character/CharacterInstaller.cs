using Gameplay;
using Gameplay.Controllers;
using Gameplay.Controllers.AttackAction;
using Modules.PlayerController;
using UnityEngine;

namespace Game.Scripts.GameObject.Enemy
{
    public class CharacterInstaller : Installer
    {
        [SerializeField] private Entity _entity;
        [SerializeField] private CharacterController2D _character;
        [SerializeField] private int _damage;
        [SerializeField] private int _health;
        [SerializeField] private LayerMask _damageLayer;
        [SerializeField] private LayerMask _throwItemLayer;
        [SerializeField] private Transform _hitEffect;
        [SerializeField] private Transform _jumpPrefab;
        [SerializeField] private Transform _rollPrefab;
        [SerializeField] private Transform _smashPrefab;
        [SerializeField] private Transform _deathPrefab;

        public override void Install(DiContainer container)
        {
            container.BindSingle(_entity);
            container.BindSingle(_character);
            container.BindInterfacesAndSelf(new HealthComponent(_health));
            container.BindInterfacesAndSelf(new CharacterDeathObserver());
            container.BindInterfacesAndSelf(new AttackComponent());
            container.BindInterfacesAndSelf(new DashComponent());
            container.BindInterfacesAndSelf(new ImpulseComponent());
            container.BindInterfacesAndSelf(new JumpComponent());
            container.BindInterfacesAndSelf(new ThrowItemComponent(_throwItemLayer));
            container.BindInterfacesAndSelf(new SmashComponent());
            container.BindInterfacesAndSelf(new TargetSensor(_damageLayer));
            container.BindInterfacesAndSelf(new DealDamageAction(_damage));
            container.BindInterfacesAndSelf(new SpawnHitEffectAction(_hitEffect));

            container.BindInterfacesAndSelf(new SpawnDashEffectAction(_rollPrefab));
            container.BindInterfacesAndSelf(new SpawnJumpEffectAction(_jumpPrefab));
            container.BindInterfacesAndSelf(new SpawnSmashEffectAction(_smashPrefab));
            container.BindInterfacesAndSelf(new SmashImpulseAction(_damageLayer));
            container.BindInterfacesAndSelf(new SpawnDeathEffectAction(_deathPrefab));
        }
    }
}