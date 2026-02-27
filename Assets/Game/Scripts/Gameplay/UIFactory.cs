using Equipment;
using Inventories;
using UnityEngine;
using Zenject;

public class UIFactory
{
    private readonly DiContainer _container;
    private readonly Canvas _canvas;

    public UIFactory(DiContainer container, Canvas canvas)
    {
        _container = container;
        _canvas = canvas;
    }

    public (GameObject inventory, GameObject equipment) CreateInventoryWithEquipment(Entity entity)
    {
        var inventory = CreateInventory(entity);
        var equipment = CreateEquipment(entity);

        SetupControllers(entity, inventory, equipment);

        return (inventory, equipment);
    }

    private GameObject CreateInventory(Entity entity)
    {
        var inventoryPrefab = entity.ResolveComponent<InventoryView>(UIPrefabs.InventoryPrefab);
        return _container.InstantiatePrefabForComponent<InventoryView>(inventoryPrefab, _canvas.transform).gameObject;
    }

    private GameObject CreateEquipment(Entity entity)
    {
        var equipmentPrefab = entity.ResolveComponent<EquipmentView>(UIPrefabs.EquipmentPrefab);
        return _container.InstantiatePrefabForComponent<EquipmentView>(equipmentPrefab, _canvas.transform).gameObject;
    }

    private void SetupControllers(Entity entity, GameObject inventory, GameObject equipment)
    {
        var inventoryContext = inventory.GetComponent<GameObjectContext>();
        var equipmentContext = equipment.GetComponent<GameObjectContext>();

        SetupEquipmentEnableController(inventoryContext, equipmentContext);
        SetupCharacterEquipmentController(entity, equipmentContext);
    }

    private void SetupEquipmentEnableController(GameObjectContext inventoryContext, GameObjectContext equipmentContext)
    {
        var inventoryPresenter = inventoryContext.Container.Resolve<InventoryPresenter>();
        var equipmentPresenter = equipmentContext.Container.Resolve<EquipmentPresenter>();

        var controller = new EquipmentEnableController(inventoryPresenter, equipmentPresenter);
        inventoryContext.Container.BindInterfacesAndSelfTo<EquipmentEnableController>()
            .FromInstance(controller)
            .AsSingle();
        controller.Initialize();
    }

    private void SetupCharacterEquipmentController(Entity entity, GameObjectContext equipmentContext)
    {
        var characterEquipment = entity.ResolveComponent<CharacterEquipment>();
        var equipmentModel = equipmentContext.Container.Resolve<EquipmentModel>();

        var controller = new CharacterEquipmentController(equipmentModel, characterEquipment);
        equipmentContext.Container.BindInterfacesAndSelfTo<CharacterEquipmentController>()
            .FromInstance(controller)
            .AsSingle();
        controller.Initialize();
    }
}