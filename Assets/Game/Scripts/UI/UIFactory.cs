using System.Collections.Generic;
using System.Linq;
using Equipment;
using Inventories;
using Inventories.Scripts;
using UnityEngine;
using Zenject;

public class UIFactory
{
    private readonly DiContainer _container;
    private readonly Canvas _canvas;
    private readonly UIPrefabCatalog _prefabCatalog;
    private readonly PlayerCharacterProvider _playerCharacterProvider;

    public UIFactory(
        DiContainer container,
        Canvas canvas,
        UIPrefabCatalog prefabCatalog,
        PlayerCharacterProvider playerCharacterProvider
    )
    {
        _container = container;
        _canvas = canvas;
        _prefabCatalog = prefabCatalog;
        _playerCharacterProvider = playerCharacterProvider;
    }

    #region Inventory

    public InventoryView CreateInventory(int equipmentId, Vector2Int inventorySize, List<SceneItem> sceneItems)
    {
        var characterEntity = _playerCharacterProvider.GetCharacterEntity();
        DiContainer characterContext = characterEntity.Context.Container;

        // Create and bind model
        var initialItems = sceneItems.Select(x => x.itemSettings).ToList();
        var model = new InventoryModel(inventorySize.x, inventorySize.y, initialItems);
        
        characterContext
            .BindInterfacesAndSelfTo<InventoryModel>()
            .FromInstance(model)
            .AsSingle();

        // Create and bind presenter
        var presenter = new InventoryPresenter(model);
        presenter.Initialize();
        
        characterContext
            .BindInterfacesAndSelfTo<InventoryPresenter>()
            .FromInstance(presenter)
            .AsSingle();

        // Create and bind view
        var prefab = _prefabCatalog.GetPrefabComponent<InventoryView>(UIPrefabs.InventoryPrefab);
        
        characterContext
            .BindInterfacesAndSelfTo<InventoryView>()
            .FromComponentInNewPrefab(prefab)
            .UnderTransform(_canvas.transform)
            .AsSingle()
            .WithArguments(presenter, _container, _container.Resolve<SignalBus>())
            .NonLazy();

        var view = characterContext.Resolve<InventoryView>();

        view.SetEquipmentID(equipmentId);

        CreateInventoryControllers(view, model);

        return view;
    }

    private void CreateInventoryControllers(InventoryView view, InventoryModel model)
    {
        var characterEntity = _playerCharacterProvider.GetCharacterEntity();
        DiContainer characterContext = characterEntity.Context.Container;

        // Audio controller
        characterContext
            .BindInterfacesAndSelfTo<InventoryAudioController>()
            .AsSingle()
            .WithArguments(model)
            .NonLazy();

        characterContext.Resolve<InventoryAudioController>().Initialize();

        // Visibility controller
        characterContext
            .BindInterfacesAndSelfTo<InventoryEnableController>()
            .AsSingle()
            .WithArguments(view)
            .NonLazy();

        characterContext.Resolve<InventoryEnableController>().Initialize();
    }

    #endregion

    #region Equipment

    public EquipmentView CreateEquipment()
    {
        var characterEntity = _playerCharacterProvider.GetCharacterEntity();
        DiContainer characterContext = characterEntity.Context.Container;

        // Create and bind model
        var model = new EquipmentModel();
        
        characterContext
            .BindInterfacesAndSelfTo<EquipmentModel>()
            .FromInstance(model)
            .AsSingle();

        // Create and bind presenter
        var presenter = new EquipmentPresenter(model);
        presenter.Initialize();
        
        characterContext
            .BindInterfacesAndSelfTo<EquipmentPresenter>()
            .FromInstance(presenter)
            .AsSingle();

        // Create and bind view
        var prefab = _prefabCatalog.GetPrefabComponent<EquipmentView>(UIPrefabs.EquipmentPrefab);
        
        characterContext
            .BindInterfacesAndSelfTo<EquipmentView>()
            .FromComponentInNewPrefab(prefab)
            .UnderTransform(_canvas.transform)
            .AsSingle()
            .WithArguments(presenter)
            .NonLazy();

        var view = characterContext.Resolve<EquipmentView>();

        CreateEquipmentControllers(view, model);

        return view;
    }

    private void CreateEquipmentControllers(EquipmentView view, EquipmentModel model)
    {
        var characterEntity = _playerCharacterProvider.GetCharacterEntity();
        DiContainer characterContext = characterEntity.Context.Container;

        // Audio controller
        characterContext
            .BindInterfacesAndSelfTo<EquipmentAudioController>()
            .AsSingle()
            .WithArguments(model)
            .NonLazy();

        characterContext.Resolve<EquipmentAudioController>().Initialize();

        // Character equipment controller
        characterContext
            .BindInterfacesAndSelfTo<CharacterEquipmentController>()
            .AsSingle()
            .WithArguments(model)
            .NonLazy();

        characterContext.Resolve<CharacterEquipmentController>().Initialize();

        // Visibility controller
        characterContext
            .BindInterfacesAndSelfTo<EquipmentEnableController>()
            .AsSingle()
            .WithArguments(view)
            .NonLazy();

        characterContext.Resolve<EquipmentEnableController>().Initialize();
    }

    #endregion
}