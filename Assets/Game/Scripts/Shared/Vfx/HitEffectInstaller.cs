using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

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
            // Was hardcoded 3f, which made the serialized field inert and pinned the pool turnover
            // rate: a short impact effect held its pooled slot for three seconds.
            // _lifeTime is the ceiling now: an effect whose particles have all died sooner goes back
            // to the pool then, instead of staying alive (and simulated) until the ceiling.
            entity.AddLifeTime(new CooldownTimer(ParticleLifetime.Resolve(gameObject, _lifeTime)));
            entity.AddDestroyAction(new InlineAction(() => SpawnUseCase.UnSpawnEntity(_gameContext.GetGameFactory(), entity)));
            entity.AddBehaviour<LifeTimeBehaviour>();
        }
    }
}