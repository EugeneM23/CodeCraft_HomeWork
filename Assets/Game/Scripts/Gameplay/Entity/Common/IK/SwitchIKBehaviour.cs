using Atomic.Elements;
using Atomic.Entities;

namespace Game.Gameplay
{
    public class SwitchIKBehaviour : IEntityInit
    {
        private IReactiveVariable<IEntity> _weapon;
        private IKHandController _IKController;

        private void OnWeaponChanged(IEntity weapon)
        {
            if (_IKController == null) return;

            if (weapon.TryGetIsIKEnable(out var enable))
                _IKController.Enable(enable.Value);
        }

        public void Init(in IEntity entity)
        {
            if (entity.TryGetIKHandController(out var ikController))
                _IKController = ikController;

            _weapon = entity.GetWeapon();
            _weapon.Observe(OnWeaponChanged);
        }
    }
}