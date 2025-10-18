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
            container.BindSingle(new SensorComponent());
            container.BindSingle(new PushAbilityController());

            container.BindInterface<PushAbility.IAction>(new TestPushAction01());
            container.BindInterface<PushAbility.IAction>(new TestPushAction02());
        }
    }
}