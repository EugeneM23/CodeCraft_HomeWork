using System;
using UnityEngine;

namespace Gameplay
{
    public abstract class Context : MonoBehaviour
    {
        [SerializeField] protected Installer[] _installers;
        public diContainer Container { get; private set; }


        public virtual void Initialize(diContainer parent = null)
        {
            "Initialize".Log(this);
            Container = new diContainer(parent);
            InstallBindings();
        }

        protected virtual void InstallBindings()
        {
            foreach (Installer installer in _installers)
                Container.Install(installer);
        }
    }
}