using SampleGame.Gameplay;
using UnityEngine;
using Zenject;

namespace Game.Scripts.Gameplay.Units
{
    [CreateAssetMenu(fileName = "SerializerInstaller", menuName = "Zenject/App/SerializerInstaller")]
    public class SerializersInstaller : ScriptableObjectInstaller
    {
        public override void InstallBindings()
        {
            Container
                .BindInterfacesAndSelfTo<EntityWorldSerializer>()
                .AsSingle()
                .NonLazy();
        }
    }
}