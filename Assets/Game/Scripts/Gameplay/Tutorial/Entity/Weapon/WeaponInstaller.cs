using Atomic.Elements;
using Atomic.Entities;
using Game.Gameplay;
using UnityEngine;

namespace Game
{
    public class WeaponInstaller : SceneEntityInstaller
    {
        [SerializeField] private SceneEntity _character;
        [SerializeField] private int _damage;
        [SerializeField] private int _ammo = 10;
        [SerializeField] private Transform _firePoint;
        [SerializeField] private Animator _animator;

        public override void Install(IEntity entity)
        {
            entity.AddAmmo(new ReactiveInt(_ammo));
            entity.AddDamage(new Const<int>(_damage));
            entity.SetFirePoint(_firePoint);
            entity.AddAnimator(_animator);

            entity.AddFireEvent(new BaseEvent());
            entity.AddFireCondition(new BaseFunction<bool>(_character.IsAlive));
            entity.AddFireAction(new CharacterFireAction(entity, GameContext.Instance));

            entity.AddBehaviour(new FireAnimBehaviour());
        }
    }
}