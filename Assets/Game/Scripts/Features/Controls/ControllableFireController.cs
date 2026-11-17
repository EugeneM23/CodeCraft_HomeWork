using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Fire input in a vehicle: the left mouse button fires the main gun, the right one the
    /// secondary gun, each for as long as it is held and the gun is ready. On foot the space bar
    /// fires (PlayerCharacterFireController); in a vehicle only the mouse does.
    ///
    /// Input is checked before anything else, so an idle frame costs two button queries.
    /// </summary>
    public sealed class ControllableFireController : IPlayerContextInit, IPlayerContextDispose, IPlayerContextLateTick
    {
        private const int MAIN_BUTTON = 0;
        private const int SECONDARY_BUTTON = 1;

        private Subscription<IEntity> _characterSubscription;

        private IExpression<bool> _mainCondition;
        private IAction _mainAction;
        private IExpression<bool> _secondaryCondition;
        private IAction _secondaryAction;

        public void Init(IPlayerContext context)
        {
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
            if (Input.GetMouseButton(MAIN_BUTTON))
                Fire(_mainCondition, _mainAction);

            if (Input.GetMouseButton(SECONDARY_BUTTON))
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
