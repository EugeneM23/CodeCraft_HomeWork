using System.Collections.Generic;
using Inventories;
using UnityEngine;
using Zenject;

public class PlayerCharacterInstaller : MonoInstaller
{
    [SerializeField] private InventoryView _inventoryPrefab;
    [SerializeField] private EquipmentView _equipmentPrefab;

    public override void InstallBindings()
    {
        Container.Bind<InventoryView>()
            .WithId(UIPrefabs.InventoryPrefab)
            .FromComponentInNewPrefab(_inventoryPrefab)
            .AsSingle();

        Container.Bind<EquipmentView>()
            .WithId(UIPrefabs.EquipmentPrefab)
            .FromComponentInNewPrefab(_equipmentPrefab)
            .AsSingle();
    }
}