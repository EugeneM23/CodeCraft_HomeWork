using UnityEngine;

namespace Gameplay
{
    public class GameObjectContext : Context
    {
        internal void Initialize(DiContainer parent)
        {
            Container = new DiContainer(parent);
            InstallBindings();
        }

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