using UnityEngine;

namespace Gameplay
{
    public partial class DiContainer
    {
        public T InstantiatePrefab<T>(T prefab, Vector3 position, Quaternion rotation) where T : Component
        {
            var instance = GameObject.Instantiate(prefab, position, rotation);

            var context = instance.GetComponentInChildren<GameObjectContext>();
            
            if (context != null) 
                Resolve<TickableManager>()?.RunAndInitialize(context.Container);

            return instance;
        }
    }
}