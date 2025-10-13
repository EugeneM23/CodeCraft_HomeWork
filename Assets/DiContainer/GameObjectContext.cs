using UnityEngine;

namespace Gameplay
{
    public class GameObjectContext : Context
    {
        protected override void InstallBindings()
        {
            base.InstallBindings();
            InjectObject();
        }

        public void InjectObject()
        {
            MonoBehaviour[] components = transform.GetComponentsInChildren<MonoBehaviour>(true);

            foreach (MonoBehaviour component in components)
                Container.Inject(component.Log());
        }
    }
}