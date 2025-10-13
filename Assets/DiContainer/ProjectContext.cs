using UnityEngine;

namespace Gameplay
{
    public class ProjectContext : Context
    {
        protected override void InstallBindings()
        {
            //Container.BindInstance<ILogger>(new ConsoleLogger());
        }
    }
}