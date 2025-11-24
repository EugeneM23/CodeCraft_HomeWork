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
        [SerializeField] private BulletSystemInstaller _bulletInstaller;
        [SerializeField] private WeaponCatalog _weapons;
        [SerializeField] private SceneEntity _audioPrefab;

        protected override void Install(IGameContext context)
        {
            _bulletInstaller.Install(context);

            context.AddPlayerCamera(_camera);
            context.AddWeaponCatalog(_weapons);
            context.AddPlayerCharacter(new ReactiveVariable<IEntity>(_player));

            GameObject audioRoot = new GameObject("AudioPool");
            context.AddAudioPool(new SceneEntityPool(_audioPrefab, audioRoot.transform));
        }
    }
}