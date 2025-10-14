using System;
using System.Collections.Generic;

namespace Gameplay
{
    public partial class DiContainer
    {
        public event Action<object> OnServiceDisposed;

        private readonly Dictionary<Type, object> _services = new();
        private readonly DiContainer _parent;
        private bool _disposed;

        public DiContainer(DiContainer parent = null) => _parent = parent;

        public void Install(Installer installer)
        {
            if (installer == null) return;
            {
                installer.Install(this);
            }

            foreach (var service in _services.Values)
                Inject(service);
        }
    }
}