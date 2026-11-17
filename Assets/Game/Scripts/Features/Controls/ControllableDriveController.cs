using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Drive input for a vehicle the player sits in: the same keys as walking, read the tank way.
    /// Vertical is the throttle along the hull, horizontal turns the hull on the spot.
    ///
    /// Idle while Character has no DriveInput (on foot, PlayerMoveController has the keys).
    /// The stick of the vehicle being left is zeroed on the way out, so an abandoned tank does not
    /// keep the throttle it was given last.
    /// </summary>
    public sealed class ControllableDriveController : IPlayerContextInit, IPlayerContextDispose, IPlayerContextLateTick
    {
        private Subscription<IEntity> _characterSubscription;
        private IReactiveVariable<Vector2> _driveInput;
        private IReactiveVariable<bool> _driveBoost;

        public void Init(IPlayerContext context)
        {
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
            if (_driveInput == null)
                return;

            _driveInput.Value = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));

            if (_driveBoost != null)
                _driveBoost.Value = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        }
    }
}
