using AudioEngine;
using Gameplay;
using Gameplay.Controllers;
using Gameplay.Controllers.AttackAction;
using Modules.PlayerController;
using UnityEngine;
using UnityEngine.Serialization;

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

        [FormerlySerializedAs("_slashSound")] [SerializeField]
        private AudioEventKey _attackSound;

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

            container.BindInterfacesAndSelf(new DealDamageAttackAction(_damage));
            container.BindInterfacesAndSelf(new SpawnHitEffectAttackAction(_hitEffect));
            container.BindInterfacesAndSelf(new SpawnDashEffectAction(_rollPrefab));
            container.BindInterfacesAndSelf(new SpawnJumpEffectAction(_jumpPrefab));
            container.BindInterfacesAndSelf(new SpawnSmashEffectAction(_smashPrefab));
            container.BindInterfacesAndSelf(new SmashImpulseAction(_damageLayer));
            container.BindInterfacesAndSelf(new SpawnDeathEffectAction(_deathPrefab));

            container.BindInterfacesAndSelf(new AttackSfxAttackAction());
            container.BindInterfacesAndSelf(new JumpSfxAttackAction());
            container.BindInterfacesAndSelf(new DeathSfxAction());
            container.BindInterfacesAndSelf(new DashSfxAttackAction());
            container.BindInterfacesAndSelf(new ThrowItemSfxAction());
            container.BindInterfacesAndSelf(new SmashSFXAction());

            container.BindInterfacesAndSelf(new LandingSFXComponent());
            container.BindInterfacesAndSelf(new StepSFXComponent());
        }
    }
}