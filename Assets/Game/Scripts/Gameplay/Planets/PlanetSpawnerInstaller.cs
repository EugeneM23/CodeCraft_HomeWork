using Game.Views;
using UnityEngine;
using Zenject;

namespace Game.Gameplay
{
    public class PlanetSpawnerInstaller : MonoInstaller
    {
        [SerializeField] private PlanetView _planetPrefab;
        [SerializeField] private RectTransform _parentTransform;

        public override void InstallBindings()
        {
            Container
                .BindInterfacesAndSelfTo<PlanetSpawner>()
                .AsSingle()
                .WithArguments(_planetPrefab, _parentTransform)
                .NonLazy();
        }
    }
}