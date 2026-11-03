using Atomic.Elements;
using Atomic.Entities;
using Modules.Gameplay;
using UnityEngine;
using Cooldown = Modules.Gameplay.Cooldown;

namespace Game
{
    public class AudioSourceInstaller : MonoEntityInstaller
    {
        [SerializeField] private AudioSource _audioSource;

        public override void Install(IEntity entity)
        {
            entity.AddEntityID(GameFactoryID.AudioSource.ToString());
            entity.AddAudioSource(_audioSource);
            entity.AddLifeTime(new Cooldown(1f));
            entity.AddBehaviour<LifeTimeBehaviour>();
            entity.AddDestroyAction(new InlineAction(() =>
                GameContext.Instance.GetGameFactory().Destroy(entity)));
        }
    }
}