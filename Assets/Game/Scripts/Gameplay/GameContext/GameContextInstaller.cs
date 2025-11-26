using System.Collections.Generic;
using Atomic.Contexts;
using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public class GameContextInstaller : SceneContextInstaller<IGameContext>
    {
        [SerializeField] private SceneEntity _camera;
        [SerializeField] private SceneEntity _player;
        [SerializeField] private WeaponCatalog _weapons;
        [SerializeField] private Transform _poolsParent;
        [SerializeField] private SceneEntity _audioPrefab;
        [SerializeField] private SceneEntity _hitEffectPrefab;

        protected override void Install(IGameContext context)
        {
            context.AddPlayerCamera(_camera);
            context.AddWeaponCatalog(_weapons);
            context.AddPlayerCharacter(new ReactiveVariable<IEntity>(_player));
            context.AddGameFactory(new GameFactory(_poolsParent));
            context.GetGameFactory().RegisterNewPrefab(_audioPrefab, GameFactoryID.AudioSource.ToString());
        }
    }
}