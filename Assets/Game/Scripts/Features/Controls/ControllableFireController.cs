using Atomic.Elements;
using Atomic.Entities;

namespace Game
{
    /// <summary>
    /// Fire input in a vehicle, off IPlayerInput: FireMain fires the main gun, FireSecondary the
    /// secondary one, each for as long as it is held and the gun is ready.
    ///
    /// Input is checked before anything else, so an idle frame costs two reads.
    /// </summary>
    public sealed class ControllableFireController : IPlayerContextInit, IPlayerContextDispose, IPlayerContextLateTick
    {
        private Subscription<IEntity> _characterSubscription;
        private IPlayerInput _input;

        private IExpression<bool> _mainCondition;
        private IAction _mainAction;
        private IExpression<bool> _secondaryCondition;
        private IAction _secondaryAction;

        public void Init(IPlayerContext context)
        {
            context.TryGetPlayerInput(out _input);
            _characterSubscription = context.GetCharacter().Observe(OnCharacterChanged);
        }

        public void Dispose(IPlayerContext context) => _characterSubscription.Dispose();

        private void OnCharacterChanged(IEntity character)
        {
            _mainCondition = null;
            _mainAction = null;
            _secondaryCondition = null;
            _secondaryAction = null;

            if (character == null || !character.HasVehicleTag())
                return;

            character.TryGetFireCondition(out _mainCondition);
            character.TryGetFireAction(out _mainAction);
            character.TryGetSecondaryFireCondition(out _secondaryCondition);
            character.TryGetSecondaryFireAction(out _secondaryAction);
        }

        public void LateTick(IPlayerContext context, float deltaTime)
        {
            if (_input == null)
                return;

            if (_input.FireMain)
                Fire(_mainCondition, _mainAction);

            if (_input.FireSecondary)
                Fire(_secondaryCondition, _secondaryAction);
        }

        private static void Fire(IExpression<bool> condition, IAction action)
        {
            if (action == null)
                return;

            if (condition != null && !condition.Invoke())
                return;

            action.Invoke();
        }
    }
}
