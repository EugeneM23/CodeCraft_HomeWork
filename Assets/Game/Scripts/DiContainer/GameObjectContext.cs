using UnityEngine;

namespace Gameplay
{
    [DefaultExecutionOrder(0)]
    public class GameObjectContext : Context
    {
        protected override void InstallBindings()
        {
            base.InstallBindings();
            InjectChildren();
        }

        private void InjectChildren()
        {
            foreach (var component in GetComponentsInChildren<MonoBehaviour>(true))
                Container.Inject(component);
        }
    }
}