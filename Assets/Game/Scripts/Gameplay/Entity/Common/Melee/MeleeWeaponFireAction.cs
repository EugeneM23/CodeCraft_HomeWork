using Atomic.Elements;
using Modules.Gameplay;

namespace Game
{
    public class MeleeWeaponFireAction : IAction
    {
        private readonly Cooldown _cooldown;

        public MeleeWeaponFireAction(Cooldown cooldown)
        {
            _cooldown = cooldown;
        }

        public void Invoke()
        {
            _cooldown.Reset();
        }
    }
}