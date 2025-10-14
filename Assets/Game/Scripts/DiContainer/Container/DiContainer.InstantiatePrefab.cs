using UnityEngine;

namespace Gameplay
{
    public partial class DiContainer
    {
        public T InstantiatePrefab<T>(T prefab, Vector3 position, Quaternion rotation) where T : Component
        {
            var instance = GameObject.Instantiate(prefab, position, rotation);

            if (instance.GetComponentInChildren<GameObjectContext>() is { } context)
            {
                context.SetParent(this);
                Resolve<TickableManager>()?.RunAndInitialize(context.Container);
            }

            return instance;
        }
    }
}