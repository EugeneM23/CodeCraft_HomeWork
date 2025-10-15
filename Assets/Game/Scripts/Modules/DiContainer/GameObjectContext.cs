using System;
using UnityEngine;

namespace Gameplay
{
    [DefaultExecutionOrder(0)]
    public class GameObjectContext : MonoBehaviour
    {
        [SerializeField] private Installer[] installers;

        public DiContainer Container { get; private set; }

        private void Awake()
        {
            var parentContainer = SceneContext.Instance?.Container;
            InitializeContainer(parentContainer);
            InstallBindings();
            
            InjectHierarchy();
        }

        public void InitializeContainer(DiContainer parent)
        {
            Container = new DiContainer(parent);

            foreach (var installer in installers)
            {
                if (installer == null)
                    throw new NullReferenceException($"Installer is null on {name}");

                Container.Install(installer);
            }
        }

        public void InstallBindings()
        {
            var tickableManager = Container.Resolve<TickableManager>();
            tickableManager?.RunAndInitialize(Container);
        }

        public void InjectHierarchy()
        {
            foreach (var component in GetComponentsInChildren<MonoBehaviour>(true)) 
                Container.Inject(component);
        }

        private void OnDestroy() => Container?.Dispose();
    }
}