using System;
using System.Collections.Generic;
using Atomic.Elements;
using Atomic.Entities;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;

namespace Game
{
    public class RangeWeaponInstaller : MonoEntityInstaller
    {
        [SerializeField] private WeaponID _id;
        [SerializeField] private CooldownTimer _fireRate;
        [SerializeField] private int _damage;

        [SerializeField] private Transform _firePoint;
        [SerializeField] private MonoEntity _bulletPrefab;

        [Tooltip("Max horizontal deviation of each bullet from the fire point's forward, in degrees to either side.")]
        [SerializeField, Range(0f, 45f)] private float _spread;

        [SerializeField] private bool _hasAmmo = true;
        [ShowIf("_hasAmmo")] [SerializeField] private int _ammoCount = 50;
        [Tooltip("Where spent casings leave the weapon. Casings do not need an ammo count: the tank's cannon has none and still throws one per shot.")]
        [SerializeField] private Transform _shellPoint;
        [SerializeField] private MonoEntity _shellPrefab;

        [SerializeField] private CameraShakeArgs _shakeArgs;

        [SerializeField] private RuntimeAnimatorController _animController;

        private GameContext _gameContext;

        /// <summary>The pooled bullet each shot fires. For CombatPoolPrewarm, which reads catalog prefabs too.</summary>
        public MonoEntity BulletPrefab => _bulletPrefab;

        /// <summary>The pooled casing each shot throws, or null: only with both a prefab and a point to throw it from.</summary>
        public MonoEntity ShellPrefab => _shellPoint != null ? _shellPrefab : null;

        /// <summary>Seconds between shots: the cooldown. Zero fires every frame.</summary>
        public float FireInterval => _fireRate != null ? _fireRate.Duration : 0f;

        public override void Install(IEntity entity)
        {
            _gameContext = GameContext.Instance;

            //Core
            entity.AddBulletPrefab(_bulletPrefab);
            if (_shellPrefab != null)
                entity.AddShellPrefab(_shellPrefab);
            entity.AddWeaponId(_id);
            entity.AddRangeWeaponTag();
            entity.AddFirePoint(_firePoint);
            entity.AddSpread(new Const<float>(_spread));

            //Animation
            entity.AddAnimationController(_animController);
            entity.AddTransform(transform);

            //Movement
            entity.AddMoveCondition(new AndExpression(new ExpressionMember<bool>(() => true)));

            //Fire
            entity.AddDamage(new ReactiveInt(_damage));
            entity.AddWeaponCooldown(_fireRate);
            entity.AddFireEvent(new Atomic.Elements.Event());
            entity.AddFireAction(new RangeWeaponFireAction(entity, _gameContext));
            entity.AddFireCondition(new AndExpression());

            if (_hasAmmo)
            {
                entity.AddAmmo(new Ammo(_ammoCount));
                entity.GetFireCondition().Add(() => entity.GetAmmo().GetCount() > 0);
            }

            // A launcher ejects no casings: without a shell prefab there is no ShellPoint, so
            // RangeWeaponFireAction skips the casing instead of spawning null. Independent of the
            // ammo count, which the tank's cannon does not have.
            if (_shellPoint != null && _shellPrefab != null)
                entity.AddShellPoint(_shellPoint);

            entity.GetFireCondition().Add(entity.GetWeaponCooldown().IsExpired);

            //CameraShake
            entity.AddCameraShakeArgs(_shakeArgs);

            entity.AddBehaviour<TickCooldownBehaviour>();
        }

    }
}