using UnityEngine;

namespace Gameplay
{
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

        public void SetParent(DiContainer diContainer)
        {
            Container = new DiContainer(diContainer);
        }
    }
}