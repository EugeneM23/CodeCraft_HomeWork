using System;
using Game.Scripts.Player;
using UnityEngine;

namespace Gameplay
{
    [DefaultExecutionOrder(-999)]
    public class SceneContext : Context
    {
        private void Awake() => Initialize();

        protected override void InstallBindings()
        {
            base.InstallBindings();

            MonoBehaviour[] allObjects = FindObjectsOfType<MonoBehaviour>();

            InjectScene(allObjects);

            InitializeGameObjectContexts(allObjects);

            CreatetickableManager();
        }

        public void InjectScene(MonoBehaviour[] allObjects)
        {
            foreach (MonoBehaviour component in allObjects)
                Container.Inject(component);
        }

        private void InitializeGameObjectContexts(MonoBehaviour[] allObjects)
        {
            foreach (var item in allObjects)
            {
                if (item.TryGetComponent(out GameObjectContext context))
                {

                    context.Initialize(Container);
                }
            }
        }

        private void CreatetickableManager()
        {
            GameObject go = new GameObject("TickableManager");
            TickableManager manager = go.AddComponent<TickableManager>();
            Container.Add(manager);
            manager.Run(Container);
        }
    }
}