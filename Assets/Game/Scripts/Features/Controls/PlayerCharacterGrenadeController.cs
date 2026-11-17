using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Grenade input: the throw key throws a grenade of the selected type at the point under the
    /// cursor, the switch key selects the next type.
    ///
    /// The cursor is projected onto a horizontal plane through the character's feet, the same way
    /// PlayerAimController aims, so throwing works over floor without colliders too.
    /// Input is checked before anything else, so an idle frame costs one key query.
    /// </summary>
    public sealed class PlayerCharacterGrenadeController : IPlayerContextInit, IPlayerContextDispose, IPlayerContextLateTick
    {
        private const KeyCode THROW_KEY = KeyCode.Q;
        private const KeyCode SWITCH_KEY = KeyCode.Tab;

        private Subscription<IEntity> _characterSubscription;

        private IExpression<bool> _throwCondition;
        private IAction<Vector3> _throwAction;
        private IAction _switchAction;
        private Transform _characterTransform;
        private UnityEngine.Camera _camera;

        public void Init(IPlayerContext context)
        {
            _characterSubscription = context.GetCharacter().Observe(OnCharacterChanged);
            _camera = UnityEngine.Camera.main;
        }

        public void Dispose(IPlayerContext context) => _characterSubscription.Dispose();

        private void OnCharacterChanged(IEntity character)
        {
            // A character without GrenadeThrowerInstaller simply cannot throw.
            _throwCondition = null;
            _throwAction = null;
            _switchAction = null;
            _characterTransform = null;

            if (character == null || !character.TryGetGrenadeThrowAction(out _throwAction))
                return;

            character.TryGetGrenadeThrowCondition(out _throwCondition);
            character.TryGetGrenadeSwitchAction(out _switchAction);
            _characterTransform = character.GetTransform();
        }

        public void LateTick(IPlayerContext context, float deltaTime)
        {
            if (Input.GetKeyDown(SWITCH_KEY))
                _switchAction?.Invoke();

            if (!Input.GetKeyDown(THROW_KEY)) return;
            if (_throwAction == null || _characterTransform == null) return;
            if (!TryGetCursorPoint(out Vector3 target)) return;
            if (_throwCondition != null && !_throwCondition.Invoke()) return;

            _throwAction.Invoke(target);
        }

        private bool TryGetCursorPoint(out Vector3 point)
        {
            point = default;

            if (_camera == null)
            {
                _camera = UnityEngine.Camera.main;
                if (_camera == null) return false;
            }

            return CursorUseCase.TryProjectOnPlane(_camera, _characterTransform.position.y, out point);
        }
    }
}
