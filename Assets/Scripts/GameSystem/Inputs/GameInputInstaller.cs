using Zenject;

namespace Game
{
    public class GameInputInstaller : MonoInstaller
    {
        public override void InstallBindings()
        {
            Container.BindInterfacesAndSelfTo<InputReader>()
                .AsSingle()
                .NonLazy();
        }
    }
}