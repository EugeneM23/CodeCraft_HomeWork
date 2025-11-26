using Atomic.Elements;
using Atomic.Entities;
using Game.Gameplay;
using Modules.Gameplay;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;

namespace Game
{
    public class RangeWeaponInstaller : SceneEntityInstaller
    {
        [SerializeField] private WeaponID _id;
        [SerializeField] private Cooldown _fireRate;
        [SerializeField] private int _damage;

        [SerializeField] private Transform _firePoint;
        [SerializeField] private SceneEntity _bulletPrefab;

        [SerializeField] private bool _hasAmmo = true;
        [ShowIf("_hasAmmo")] [SerializeField] private int _ammoCount = 50;
        [ShowIf("_hasAmmo")] [SerializeField] private Transform _shellPoint;
        [ShowIf("_hasAmmo")] [SerializeField] private SceneEntity _shellPrefab;

        [SerializeField] private CameraShakeArgs _shakeArgs;

        [SerializeField] private RuntimeAnimatorController _animController;

        private GameContext _gameContext;

        public override void Install(IEntity entity)
        {
            _gameContext = GameContext.Instance;

            //Core
            entity.AddBulletPrefab(_bulletPrefab);
            entity.AddShellPrefab(_shellPrefab);
            entity.AddWeaponId(_id);
            entity.AddRangeWeaponTag();
            entity.AddFirePoint(_firePoint);

            //Animation
            entity.AddAnimationController(_animController);
            entity.AddTransform(transform);

            //Movement
            entity.AddMoveCondition(new AndExpression((() => true)));

            //Fire
            entity.AddDamage(new Const<int>(_damage));
            entity.AddWeaponCooldown(_fireRate);
            entity.AddFireEvent(new BaseEvent());
            entity.AddFireAction(new RangeWeaponFireAction(entity, _gameContext));
            entity.AddFireCondition(new AndExpression());

            if (_hasAmmo)
            {
                entity.AddAmmo(new Ammo(_ammoCount));
                entity.GetFireCondition().Append(() => entity.GetAmmo().GetCount() > 0);
                entity.AddShellPoint(_shellPoint);
            }

            entity.GetFireCondition().Append(entity.GetWeaponCooldown().IsExpired);

            //CameraShake
            entity.AddCameraShakeArgs(_shakeArgs);

            entity.OnUpdated += deltaTime => entity.GetWeaponCooldown().Tick(deltaTime);
        }
    }
}