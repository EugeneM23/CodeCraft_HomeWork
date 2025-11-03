using Game.Scripts.GameObject.Player;
using Gameplay;
using UnityEngine;

namespace Game.Scripts.GameObject
{
    public class GameInstaller : Installer
    {
        [SerializeField] private PlayerSpawner _playerSpawner;

        public override void Install(DiContainer container)
        {
            container.BindSingle(_playerSpawner);
            container.BindSingle(new PlayerCharacterProvider());
        }
    }
}