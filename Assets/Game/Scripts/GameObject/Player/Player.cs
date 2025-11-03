using Gameplay.Controllers;
using Modules.PlayerController;

namespace Gameplay
{
    public class Player : IInitializeble
    {
        private CharacterController2D _character;
        private AttackComponent _attackComponent;

        [Inject]
        public void Construct(CharacterController2D character, AttackComponent attackComponent)
        {
            _character = character;
            _attackComponent = attackComponent;
        }

        public void Initialize()
        {
            _character.AddMoveCondition(_attackComponent.IsAttacking);
        }
    }
}