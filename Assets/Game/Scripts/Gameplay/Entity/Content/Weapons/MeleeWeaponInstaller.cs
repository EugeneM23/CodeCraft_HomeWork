using Atomic.Elements;
using Atomic.Entities;
using Game.Gameplay;
using Modules.Gameplay;
using UnityEngine;

namespace Game
{
    public class MeleeWeaponInstaller : SceneEntityInstaller
    {
        [SerializeField] private WeaponID _id;
        [SerializeField] private int _damage;
        [SerializeField] private float _damageRadius = 0.5f;
        [SerializeField] private CameraShakeArgs _shakeArgs;
        [SerializeField] private LayerMask _damageLayer;
        [SerializeField] private Cooldown _cooldown;
        [SerializeField] private RuntimeAnimatorController _animController;
        private GameContext _gameContext;

        public override void Install(IEntity entity)
        {
            _gameContext = GameContext.Instance;

            //Core
            entity.AddWeaponId(_id);
            entity.AddMeleeWeaponTag();
            entity.AddTransform(transform);
            entity.AddGameObject(transform.gameObject);
            entity.AddAnimationController(_animController);

            //Damage
            entity.AddDamage(new Const<int>(_damage));
            entity.AddDamageLayer(_damageLayer);
            entity.AddDamageRadius(new Const<float>(_damageRadius));
            entity.AddDamageCastEnabled(new ReactiveVariable<bool>());

            //Attack
            entity.AddMoveCondition(new AndExpression());
            entity.AddWeaponCooldown(_cooldown);
            entity.GetMoveCondition().Append(_cooldown.IsExpired);

            entity.AddFireAction(new BaseAction(() =>
            {
                entity.GetWeaponCooldown().Reset();
                entity.GetFireEvent().Invoke();
            }));
            
            entity.AddFireEvent(new BaseEvent());

            entity.AddFireCondition(new AndExpression(() => entity.GetWeaponCooldown().IsExpired()));

            //CameraShake
            entity.AddCameraShakeArgs(_shakeArgs);

            entity.OnUpdated += deltaTime => entity.GetWeaponCooldown().Tick(deltaTime);
        }
    }
}