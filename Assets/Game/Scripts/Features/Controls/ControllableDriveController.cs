using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Drive input for the player's vehicle, off IPlayerInput: Drive is the stick (y the throttle
    /// along the hull, x turns the hull on the spot), Boost full power.
    ///
    /// Idle while Character has no DriveInput. The stick of the vehicle being left is zeroed on the
    /// way out, so a tank handed over does not keep the throttle it was given last.
    /// </summary>
    public sealed class ControllableDriveController : IPlayerContextInit, IPlayerContextDispose, IPlayerContextLateTick
    {
        private Subscription<IEntity> _characterSubscription;
        private IReactiveVariable<Vector2> _driveInput;
        private IReactiveVariable<bool> _driveBoost;
        private IPlayerInput _input;

        public void Init(IPlayerContext context)
        {
            context.TryGetPlayerInput(out _input);
            _characterSubscription = context.GetCharacter().Observe(OnCharacterChanged);
        }

        public void Dispose(IPlayerContext context)
        {
            _characterSubscription.Dispose();
            Release();
        }

        private void OnCharacterChanged(IEntity character)
        {
            Release();

            if (character != null)
            {
                character.TryGetDriveInput(out _driveInput);
                character.TryGetDriveBoost(out _driveBoost);
            }
        }

        private void Release()
        {
            if (_driveInput != null)
                _driveInput.Value = Vector2.zero;

            if (_driveBoost != null)
                _driveBoost.Value = false;

            _driveInput = null;
            _driveBoost = null;
        }

        public void LateTick(IPlayerContext context, float deltaTime)
        {
            if (_driveInput == null || _input == null)
                return;

            _driveInput.Value = _input.Drive;

            if (_driveBoost != null)
                _driveBoost.Value = _input.Boost;
        }
    }
}
