using System.Diagnostics;
using System.Linq;
using UnityEngine;
using Debug = UnityEngine.Debug;

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
                Container.Inject(component);
        }
    }
}