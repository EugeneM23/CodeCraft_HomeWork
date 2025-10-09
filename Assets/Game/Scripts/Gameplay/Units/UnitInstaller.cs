using System;
using SampleGame.Gameplay;
using SaveLoadSystem;
using UnityEngine;
using Zenject;

namespace Game.Gameplay.Units
{
    public class UnitInstaller : MonoInstaller
    {
        public override void InstallBindings()
        {
            Container
                .BindInterfacesAndSelfTo<SaveLoadProvider>()
                .AsSingle()
                .NonLazy();

            Component[] components = gameObject.GetComponents<Component>();

            foreach (Component component in components)
            {
                var type = component.GetType();
                if (SerializerCatalog.Has(type))
                {
                    Type serializer = SerializerCatalog.GetSerializer(type);

                    Container.Bind(type).FromInstance(component).AsSingle().NonLazy();
                    Container.BindInterfacesAndSelfTo(serializer).AsSingle().NonLazy();
                }
            }
        }
    }
}