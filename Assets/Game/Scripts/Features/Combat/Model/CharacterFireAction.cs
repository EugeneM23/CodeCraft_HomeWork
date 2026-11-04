using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public class CharacterFireAction : IAction
    {
        private readonly int _fire = Animator.StringToHash("Fire");
        private readonly int _melee = Animator.StringToHash("Melee");
        private readonly IEntity _character;
        private readonly IReactiveVariable<IEntity> _weapon;

        public CharacterFireAction(IEntity character)
        {
            _character = character;
            _weapon = _character.GetWeapon();
        }

        public void Invoke()
        {
            IEntity weapon = _character.GetWeapon().Value;

            if (!_weapon.Value.GetFireCondition().Invoke()) return;

            weapon.GetFireAction().Invoke();

            // Layers 1 and 2 are AC_BaseCharacter's Fire and Melee, and only the player's
            // controller has them: AC_Zombie has two layers, AC_Rapax one. Playing a layer that
            // is not there logs "Invalid Layer Index" on every swing, which went unnoticed only
            // because until recently no enemy ever reached a target to swing at. Enemies get
            // their attack animation from EnemyAttackAnimBehaviour's trigger instead.
            if (_weapon.Value.HasRangeWeaponTag())
                PlayOnLayer(_fire, 1);

            if (_weapon.Value.HasMeleeWeaponTag())
                PlayOnLayer(_melee, 2);
        }

        private void PlayOnLayer(int state, int layer)
        {
            Animator animator = _character.GetAnimator();

            if (layer < animator.layerCount)
                animator.Play(state, layer);
        }
    }
}