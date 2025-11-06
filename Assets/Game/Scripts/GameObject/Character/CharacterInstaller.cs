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

        public override void Install(DiContainer container)
        {
            // Core
            container.BindSingle(_entity);
            container.BindSingle(_character);

            //Health
            container.BindInterfacesAndSelf(new SpawnHitEffectHealthAction(_hitEffect));
            container.BindInterfacesAndSelf(new HealthComponent(_health));
            container.BindInterfacesAndSelf(new CharacterDeathObserver());

            // Movement
            container.BindInterfacesAndSelf(new DashComponent());
            container.BindInterfacesAndSelf(new ImpulseComponent());
            container.BindInterfacesAndSelf(new JumpComponent());

            // Attack
            container.BindInterfacesAndSelf(new AttackComponent());
            container.BindInterfacesAndSelf(new DealDamageAttackAction(_damage));
            container.BindInterfacesAndSelf(new AttackSfxAttackAction());
            container.BindInterfacesAndSelf(new TargetSensor(_damageLayer));


            // Smash
            container.BindInterfacesAndSelf(new SmashComponent());
            container.BindInterfacesAndSelf(new SmashImpulseAction(_damageLayer));
            container.BindInterfacesAndSelf(new SmashSFXAction());
            container.BindInterfacesAndSelf(new SpawnSmashEffectAction(_smashPrefab));

            // Throw
            container.BindInterfacesAndSelf(new ThrowItemComponent(_throwItemLayer));
            container.BindInterfacesAndSelf(new ThrowItemSfxAction());

            // Jump
            container.BindInterfacesAndSelf(new SpawnJumpEffectAction(_jumpPrefab));
            container.BindInterfacesAndSelf(new JumpSfxAttackAction());

            // Dash
            container.BindInterfacesAndSelf(new SpawnDashEffectAction(_rollPrefab));
            container.BindInterfacesAndSelf(new DashSfxAttackAction());

            // Death
            container.BindInterfacesAndSelf(new SpawnDeathEffectAction(_deathPrefab));
            container.BindInterfacesAndSelf(new DeathSfxAction());

            // Environment / Misc
            container.BindInterfacesAndSelf(new LandingSFXComponent());
            container.BindInterfacesAndSelf(new StepSFXComponent());
        }
    }
}