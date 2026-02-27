using UnityEngine;
using Zenject;

public class PlayerCharacterInstaller : MonoInstaller
{
    [SerializeField] private CharacterEquipment _equipment;

    public override void InstallBindings()
    {
        Container
            .Bind<CharacterEquipment>()
            .FromInstance(_equipment)
            .AsSingle()
            .NonLazy();
    }
}