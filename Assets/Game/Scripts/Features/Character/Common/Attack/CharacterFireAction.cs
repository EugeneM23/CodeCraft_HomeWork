using Atomic.Elements;
using Atomic.Entities;

namespace Game
{
    /// <summary>
    /// The character's attack: fires the held weapon if it is ready.
    /// </summary>
    /// <remarks>
    /// The attack animation is not played here: the weapon raises its FireEvent from the fire call,
    /// and PlayerCharacterAttackAnimBehaviour (player) or CharacterAttackAnimBehaviour (everyone else) plays
    /// it from there.
    /// </remarks>
    public class CharacterFireAction : IAction
    {
        private readonly IReactiveVariable<IEntity> _weapon;

        public CharacterFireAction(IEntity character)
        {
            _weapon = character.GetWeapon();
        }

        public void Invoke()
        {
            IEntity weapon = _weapon.Value;

            if (!weapon.GetFireCondition().Invoke()) return;

            weapon.GetFireAction().Invoke();
        }
    }
}
