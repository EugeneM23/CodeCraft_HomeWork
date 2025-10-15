using System;
using System.Collections.Generic;

namespace Gameplay
{
    public partial class DiContainer
    {
        private readonly Dictionary<Type, object> _services = new();
        private readonly DiContainer _parent;
        private bool _disposed;

        public DiContainer(DiContainer parent = null)
        {
            _parent = parent;
        }

        public void Install(Installer installer)
        {
            installer.Install(this);

            foreach (var service in _services.Values)
                Inject(service);
        }
    }
}