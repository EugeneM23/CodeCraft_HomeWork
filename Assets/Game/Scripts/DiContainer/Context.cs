using System;
using UnityEngine;

namespace Gameplay
{
    [DefaultExecutionOrder(-999)]
    public abstract class Context : MonoBehaviour
    {
        [SerializeField] private Installer[] installers;
        public DiContainer Container { get; protected set; }

        protected virtual void Awake()
        {
            DiContainer parent = GetParent();
            Container = new DiContainer(parent);

            InstallBindings();

            var tickableManager = Container.Resolve<TickableManager>();
            tickableManager?.RunAndInitialize(Container);
        }

        protected virtual DiContainer GetParent() => SceneContext.Instance?.Container;

        protected virtual void InstallBindings()
        {
            foreach (var installer in installers)
            {
                if (installer == null)
                    throw new NullReferenceException("Installer is null");

                Container.Install(installer);
            }
        }

        private void OnDestroy() => Container?.Dispose();
    }
}