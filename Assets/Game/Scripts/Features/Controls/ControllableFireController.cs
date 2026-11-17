using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Fire input in a vehicle, from the Tank map of the game's input actions: FireMain (left mouse
    /// button by default) fires the main gun, FireSecondary (right) the secondary gun, each for as
    /// long as it is held and the gun is ready. The map is off while a menu covers the battle.
    ///
    /// Input is checked before anything else, so an idle frame costs two button queries.
    /// </summary>
    public sealed class ControllableFireController : IPlayerContextInit, IPlayerContextDispose, IPlayerContextLateTick
    {
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
            GameControls.TankActions tank = AppServices.Input.Controls.Tank;
            if (tank.FireMain.IsPressed())
                Fire(_mainCondition, _mainAction);

            if (tank.FireSecondary.IsPressed())
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
