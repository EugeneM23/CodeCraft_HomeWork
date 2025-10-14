using System;
using UnityEngine;

namespace Gameplay
{
    [DefaultExecutionOrder(-999)]
    public class SceneContext : Context
    {
        public static SceneContext Instance { get; private set; }

        protected override void Awake()
        {
            Instance = this;
            base.Awake();

            InjectSceneObjects();

            TickableManager manager = CreateTickableManager();
            manager?.RunAndInitialize(Container);
        }

        private TickableManager CreateTickableManager()
        {
            GameObject tickableManager = new GameObject("TickableManager");
            var component = tickableManager.AddComponent<TickableManager>();
            Container.BindSingle(component);
            return component;
        }

        protected override DiContainer GetParent() => null;

        protected override void InstallBindings()
        {
            base.InstallBindings();
        }

        private void InjectSceneObjects()
        {
            foreach (var component in FindObjectsOfType<MonoBehaviour>())
            {
                // Пропускаем компоненты, принадлежащие GameObjectContext (они инжектятся локально в GameObjectContext)
                if (component.GetComponentInParent<GameObjectContext>() == null)
                    Container.Inject(component);
            }
        }
    }
}