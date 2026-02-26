using System.Collections.Generic;
using Inventories.Scripts;
using UnityEngine;
using Zenject;

namespace Inventories
{
    public class SceneSystemInstaller : MonoInstaller
    {
        [SerializeField] private Canvas _canvas;

        [SerializeField] private Entity _player;

        public override void InstallBindings()
        {
            Container
                .Bind<Canvas>()
                .FromInstance(_canvas)
                .AsSingle()
                .NonLazy();

            Container
                .Bind<PlayerCharacterProvider>()
                .FromNew()
                .AsSingle()
                .WithArguments(_player)
                .NonLazy();


            Container
                .Bind<DragContext>()
                .AsSingle();

            SignalBusInstaller.Install(Container);

            Container
                .DeclareSignal<ToggleEquipmentSignal>();
            
            Container
                .DeclareSignal<CloseEquipmentSignal>();
        }
    }
}