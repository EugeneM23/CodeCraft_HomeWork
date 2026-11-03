using Atomic.Elements;
using Atomic.Entities;
using Modules.Gameplay;
using UnityEngine;
using Cooldown = Modules.Gameplay.Cooldown;

namespace Game
{
    public class HitEffectInstaller : MonoEntityInstaller
    {
        [SerializeField] private float _lifeTime = 3f;
        private IGameContext _gameContext;

        public override void Install(IEntity entity)
        {
            _gameContext = GameContext.Instance;
            entity.AddEntityID(gameObject.name.Replace("(Clone)", ""));
            entity.AddTransform(transform);
            entity.AddLifeTime(new Cooldown(3f));
            entity.AddDestroyAction(new InlineAction(() => SpawnUseCase.UnSpawnEntity(_gameContext.GetGameFactory(), entity)));
            entity.AddBehaviour<LifeTimeBehaviour>();
        }
    }
}