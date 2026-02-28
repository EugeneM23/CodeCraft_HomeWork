using Equipment;
using Inventories;
using UnityEngine;
using Zenject;

public class UIFactory
{
    private readonly DiContainer _container;
    private readonly Canvas _canvas;
    private readonly UIPrefabCatalog _prefabCatalog;

    public UIFactory(DiContainer container, Canvas canvas, UIPrefabCatalog prefabCatalog)
    {
        _container = container;
        _canvas = canvas;
        _prefabCatalog = prefabCatalog;
    }

    public GameObject CreateInventory(int equipmentId)
    {
        var inventoryPrefab = _prefabCatalog.GetPrefabComponent<InventoryView>(UIPrefabs.InventoryPrefab);

        InventoryView inventory =
            _container.InstantiatePrefabForComponent<InventoryView>(inventoryPrefab, _canvas.transform);

        inventory.SetEquipmentID(equipmentId);

        return inventory.gameObject;
    }

    public EquipmentView CreateEquipment()
    {
        var equipment = new EquipmentModel();
        var equipmentPresenter = new EquipmentPresenter(equipment);

        equipmentPresenter.Initialize();

        var equipmentPrefab = _prefabCatalog.GetPrefabComponent<EquipmentView>(UIPrefabs.EquipmentPrefab);

        var view = _container.InstantiatePrefabForComponent<EquipmentView>(equipmentPrefab, _canvas.transform,
            new object[] { equipmentPresenter }
        );

        // Создаем контроллер напрямую через контейнер
        var controller = _container.Instantiate<EquipmentEnableController>(new object[] { view });
        // Вызываем Initialize вручную
        controller.Initialize();

        return view;
    }
}