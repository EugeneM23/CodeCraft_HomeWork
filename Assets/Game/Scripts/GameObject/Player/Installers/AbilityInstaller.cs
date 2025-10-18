using UnityEngine;

namespace Gameplay.Ability
{
    public class AbilityInstaller : Installer
    {
        [SerializeField] private LayerMask _groundLayer;
        [SerializeField] private Transform _transform;

        public override void Install(DiContainer container)
        {
            container.BindSingle(new PushAbility());
            container.BindSingle(new Sensor());
            container.BindSingle(new PushAbilityController());

            container.Bind<PushAbility.IAction>(new TestPushAction01());
            container.Bind<PushAbility.IAction>(new TestPushAction02());
        }
    }
}