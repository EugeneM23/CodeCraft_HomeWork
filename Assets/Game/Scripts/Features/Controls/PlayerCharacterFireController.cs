using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Fire input.
    ///
    /// Input is checked before the condition: the condition reaches the weapon cooldown and ammo
    /// through dictionary lookups and a delegate, and on an idle frame none of that is needed.
    /// </summary>
    public class PlayerCharacterFireController : IPlayerContextInit, IPlayerContextDispose, IPlayerContextLateTick
    {
        private IReactiveVariable<IEntity> _characterSource;
        private Subscription<IEntity> _characterSubscription;

        private IExpression<bool> _fireCondition;
        private IAction _fireAction;

        public void Init(IPlayerContext context)
        {
            _characterSource = context.GetCharacter();
            _characterSubscription = _characterSource.Observe(OnCharacterChanged);
        }

        public void Dispose(IPlayerContext context) => _characterSubscription.Dispose();

        /// <summary>
        /// A vehicle is fired with the mouse (ControllableFireController), not the space bar.
        /// </summary>
        private void OnCharacterChanged(IEntity character)
        {
            bool onFoot = character != null && !character.HasVehicleTag();

            _fireCondition = onFoot ? character.GetFireCondition() : null;
            _fireAction = onFoot ? character.GetFireAction() : null;
        }

        public void LateTick(IPlayerContext context, float deltaTime)
        {
            if (!Input.GetKey(KeyCode.Space)) return;
            if (_fireAction == null) return;

            if (_fireCondition != null && !_fireCondition.Invoke()) return;

            _fireAction.Invoke();
        }
    }
}
