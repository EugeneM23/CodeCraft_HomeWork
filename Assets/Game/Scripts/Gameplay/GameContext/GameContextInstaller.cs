using System.Collections.Generic;
using Atomic.Contexts;
using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;
using UnityEngine.Serialization;

namespace Game
{
    public class GameContextInstaller : SceneContextInstaller<IGameContext>
    {
        [SerializeField] private WeaponCatalog _weaponsCatalog;
        [SerializeField] private PlayerContext _playerContext; 
        [SerializeField] private Transform _poolsParent;
        [SerializeField] private SceneEntity _audioPrefab;
        [SerializeField] private SceneEntity _hitEffectPrefab;

        protected override void Install(IGameContext context)
        {
            context.AddWeaponCatalog(_weaponsCatalog);
            context.AddPlayerContext(_playerContext);
            context.AddGameFactory(new GameFactory(_poolsParent));
            context.GetGameFactory().RegisterNewPrefab(_audioPrefab, GameFactoryID.AudioSource.ToString());
        }
    }
}