using System;
using UnityEngine;
using Zenject;

public class CameraController : IInitializable, IDisposable
{
    private readonly CameraMovement _cameraMovement;
    private readonly GameScreen _gameScreen;

    public CameraController(CameraMovement cameraMovement, GameScreen gameScreen)
    {
        _cameraMovement = cameraMovement;
        _gameScreen = gameScreen;
    }

    public void Initialize()
    {
        _gameScreen.OnInventoryOpened += HandleInventoryOpened;
        _gameScreen.OnInventoryClosed += HandleInventoryClosed;
    }

    public void Dispose()
    {
        _gameScreen.OnInventoryOpened -= HandleInventoryOpened;
        _gameScreen.OnInventoryClosed -= HandleInventoryClosed;
    }

    private void HandleInventoryOpened()
    {
        _cameraMovement.MoveToInventoryPosition();
    }

    private void HandleInventoryClosed()
    {
        _cameraMovement.MoveToGameplayPosition();
    }
}