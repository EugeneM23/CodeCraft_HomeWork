using System;
using UnityEngine;

namespace Gameplay
{
    public abstract class Context : MonoBehaviour
    {
        [SerializeField] protected Installer[] _installers;
        public DiContainer Container { get; private set; }

        public virtual void Initialize(DiContainer parent = null)
        {
            Container = new DiContainer(parent);
            InstallBindings();
        }

        protected virtual void InstallBindings()
        {
            foreach (Installer installer in _installers)
            {
                Container.Install(installer);
            }
        }
    }
}