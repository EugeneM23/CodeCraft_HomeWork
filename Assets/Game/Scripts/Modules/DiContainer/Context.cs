using UnityEngine;

namespace Gameplay
{
    public abstract class Context : MonoBehaviour
    {
        [SerializeField] private Installer[] installers;
        public DiContainer Container { get; private set; }

        public virtual void Initialize(DiContainer parent = null)
        {
            Container = new DiContainer(parent);
            InstallBindings();
        }

        protected virtual void InstallBindings()
        {
            if (installers == null) return;

            foreach (var installer in installers)
                Container.Install(installer);
        }
    }
}