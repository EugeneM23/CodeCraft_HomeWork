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
        var context = GetCharacterContext();
        var initialItems = sceneItems.Select(x => x.itemSettings).ToList();
        
        var model = BindModel(context, new InventoryModel(inventorySize.x, inventorySize.y, initialItems));
        var presenter = BindPresenter(context, new InventoryPresenter(model));
        var view = BindInventoryView(context, presenter);
        
        view.gameObject.SetActive(false);
        view.SetEquipmentID(equipmentId);
        
        BindController<InventoryAudioController>(context, model);
        BindController<InventoryEnableController>(context, view);

        return view;
    }

    private InventoryView BindInventoryView(DiContainer context, InventoryPresenter presenter)
    {
        var prefab = _prefabCatalog.GetPrefabComponent<InventoryView>(UIPrefabs.InventoryPrefab);

        context
            .BindInterfacesAndSelfTo<InventoryView>()
            .FromComponentInNewPrefab(prefab)
            .UnderTransform(_canvas.transform)
            .AsSingle()
            .WithArguments(presenter, _container, _container.Resolve<SignalBus>())
            .NonLazy();

        return context.Resolve<InventoryView>();
    }

    #endregion

    #region Equipment

    public EquipmentView CreateEquipment(List<Item> initialItems = null)
    {
        var context = GetCharacterContext();

        var model = BindModel(context, initialItems != null ? new EquipmentModel(initialItems) : new EquipmentModel());
        var presenter = BindPresenter(context, new EquipmentPresenter(model));
        var view = BindEquipmentView(context, presenter);
        
        view.gameObject.SetActive(false);
        
        BindController<EquipmentAudioController>(context, model);
        BindController<CharacterEquipmentController>(context, model);
        BindController<EquipmentEnableController>(context, view);
        BindAnimSetController(context, model);

        return view;
    }

    private EquipmentView BindEquipmentView(DiContainer context, EquipmentPresenter presenter)
    {
        var prefab = _prefabCatalog.GetPrefabComponent<EquipmentView>(UIPrefabs.EquipmentPrefab);

        context
            .BindInterfacesAndSelfTo<EquipmentView>()
            .FromComponentInNewPrefab(prefab)
            .UnderTransform(_canvas.transform)
            .AsSingle()
            .WithArguments(presenter)
            .NonLazy();

        return context.Resolve<EquipmentView>();
    }

    private void BindAnimSetController(DiContainer context, EquipmentModel model)
    {
        var animator = _playerCharacterProvider.GetCharacterEntity().GetComponent<Animator>();
        
        context
            .BindInterfacesAndSelfTo<AnimSetController>()
            .AsSingle()
            .WithArguments(model, animator)
            .NonLazy();

        context.Resolve<AnimSetController>().Initialize();
    }

    #endregion

    #region Helpers

    private DiContainer GetCharacterContext()
    {
        return _playerCharacterProvider.GetCharacterEntity().Context.Container;
    }

    private T BindModel<T>(DiContainer context, T model) where T : class
    {
        context.BindInterfacesAndSelfTo<T>().FromInstance(model).AsSingle();
        return model;
    }

    private T BindPresenter<T>(DiContainer context, T presenter) where T : class
    {
        presenter.GetType().GetMethod("Initialize")?.Invoke(presenter, null);
        context.BindInterfacesAndSelfTo<T>().FromInstance(presenter).AsSingle();
        return presenter;
    }

    private void BindController<T>(DiContainer context, object arg) where T : class
    {
        context.BindInterfacesAndSelfTo<T>().AsSingle().WithArguments(arg).NonLazy();
        context.Resolve<T>().GetType().GetMethod("Initialize")?.Invoke(context.Resolve<T>(), null);
    }

    #endregion
}