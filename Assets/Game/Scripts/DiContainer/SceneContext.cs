using System;
using UnityEngine;

namespace Gameplay
{
    [DefaultExecutionOrder(-999)]
    public class SceneContext : MonoBehaviour
    {
        [SerializeField] private Installer[] installers;

        public static SceneContext Instance { get; private set; }
        public DiContainer Container { get; private set; }

        private void Awake()
        {
            Instance = this;

            InitializeContainer();

            TickableManager tickableManager = CreateTickableManager();
            Container.BindSingle(tickableManager);

            InstallBindings(tickableManager);
            InjectScene();
        }

        private void InitializeContainer()
        {
            Container = new DiContainer();

            foreach (var installer in installers)
            {
                if (installer == null)
                    throw new NullReferenceException($"Installer is null on {name}");

                Container.Install(installer);
            }
        }

        private void InstallBindings(TickableManager manager)
        {
            manager?.RunAndInitialize(Container);
        }

        private void InjectScene()
        {
            foreach (var component in FindObjectsOfType<MonoBehaviour>())
            {
                if (component.GetComponentInParent<GameObjectContext>() == null)
                    Container.Inject(component);
            }
        }

        private TickableManager CreateTickableManager()
        {
            var go = new GameObject("TickableManager");
            return go.AddComponent<TickableManager>();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            Container?.Dispose();
        }
    }
}