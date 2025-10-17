using UnityEngine;

namespace Gameplay.Ability
{
    public class AbilityInstaller : Installer
    {
        [SerializeField] private LayerMask _groundLayer;
        [SerializeField] private Transform _transform;

        public override void Install(DiContainer container)
        {
            var pushAbility = new PushAbility(_groundLayer, _transform);
            container.BindSingle(pushAbility);
            container.BindSingle(new PushAbilityController());

            container.Bind<PushAbility.IAction>(new TestPushAction01());
            container.Bind<PushAbility.IAction>(new TestPushAction02());
        }
    }
}