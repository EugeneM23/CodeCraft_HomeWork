using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Fire input in a vehicle, off IPlayerInput: FireMain fires the main gun for as long as it is
    /// held and the gun is ready; a stunned tank (TankStunUseCase) does not fire.
    ///
    /// FireSecondary: while the vehicle's auto guns are on (<see cref="CombatFocus.Enabled"/>, set by
    /// the combat targeting when the player's tank gets them) it does not fire the machine gun — that
    /// fires by itself — but points the auto guns at the cursor on the ground (<see cref="CombatFocus"/>,
    /// the same projection as PlayerAimController). Without auto guns it fires the secondary gun, as before.
    ///
    /// Input is checked before anything else, so an idle frame costs two reads.
    /// </summary>
    public sealed class ControllableFireController : IPlayerContextInit, IPlayerContextDispose, IPlayerContextLateTick
    {
        private Subscription<IEntity> _characterSubscription;
        private IPlayerInput _input;

        private IEntity _character;
        private Transform _characterTransform;
        private Camera _camera;

        private IExpression<bool> _mainCondition;
        private IAction _mainAction;
        private IExpression<bool> _secondaryCondition;
        private IAction _secondaryAction;
        private Transform _cannonFirePoint;
        private IEntity _cannon;

        public void Init(IPlayerContext context)
        {
            context.TryGetPlayerInput(out _input);
            _characterSubscription = context.GetCharacter().Observe(OnCharacterChanged);
            _camera = Camera.main; // perf-ok: cached in Init, not in a tick
        }

        public void Dispose(IPlayerContext context)
        {
            _characterSubscription.Dispose();
            CombatFocus.Clear();
        }

        private void OnCharacterChanged(IEntity character)
        {
            _character = null;
            _characterTransform = null;
            _mainCondition = null;
            _mainAction = null;
            _secondaryCondition = null;
            _secondaryAction = null;
            _cannonFirePoint = null;
            _cannon = null;
            CombatFocus.Clear();

            if (character == null || !character.HasVehicleTag())
                return;

            _character = character;
            character.TryGetTransform(out _characterTransform);
            character.TryGetFireCondition(out _mainCondition);
            character.TryGetFireAction(out _mainAction);
            character.TryGetSecondaryFireCondition(out _secondaryCondition);
            character.TryGetSecondaryFireAction(out _secondaryAction);
            if (character.TryGetWeapon(out IReactiveVariable<IEntity> cannon) && cannon.Value != null)
            {
                cannon.Value.TryGetFirePoint(out _cannonFirePoint);
                _cannon = cannon.Value;
                AmmoUseCase.GetOrAdd(_cannon);
            }
        }

        public void LateTick(IPlayerContext context, float deltaTime)
        {
            if (_input == null)
                return;

            // Q: the other shell type (HE ↔ AP) into the cannon.
            if (_cannon != null && AmmoInput.SwitchPressed())
                AmmoUseCase.Switch(_cannon);

            if (_input.FireMain && !TankStunUseCase.IsStunned(_character))
                Fire(_mainCondition, _mainAction);

            if (CombatFocus.Enabled)
                Focus(_input.FireSecondary);
            else if (_input.FireSecondary && !TankStunUseCase.IsStunned(_character))
                Fire(_secondaryCondition, _secondaryAction);
        }

        /// <summary>How far (m) along the cannon's barrel the auto guns aim while the right button is held.</summary>
        private const float CANNON_AIM_DISTANCE = 18f;

        /// <summary>
        /// Held: the auto guns aim where the cannon aims — a point along its barrel. Without a cannon, at the cursor
        /// point on the ground, at the vehicle's height.
        /// </summary>
        private void Focus(bool held)
        {
            if (!held || _characterTransform == null)
            {
                CombatFocus.Clear();
                return;
            }

            if (_cannonFirePoint != null)
            {
                CombatFocus.Set(_cannonFirePoint.position + _cannonFirePoint.forward * CANNON_AIM_DISTANCE);
                return;
            }

            if (_camera == null)
            {
                // The camera can be destroyed on scene teardown; looked up again only once it is gone.
                _camera = Camera.main; // perf-ok: re-lookup only after the cached camera is destroyed
                if (_camera == null)
                {
                    CombatFocus.Clear();
                    return;
                }
            }

            if (CursorUseCase.TryProjectOnPlane(_camera, CursorUseCase.ScreenPoint(_input.Pointer),
                    _characterTransform.position.y, out Vector3 point))
                CombatFocus.Set(point);
            else
                CombatFocus.Clear();
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
