using Atomic.Elements;
using Atomic.Entities;
using Modules.Gameplay;
using UnityEngine;

namespace Game
{
    public class AudioSourceInstaller : SceneEntityInstaller
    {
        [SerializeField] private AudioSource _audioSource;

        public override void Install(IEntity entity)
        {
            entity.AddAudioSource(_audioSource);
            entity.AddLifeTime(new Cooldown(1f));
            entity.AddBehaviour<LifeTimeBehaviour>();
            entity.AddDestroyAction(new BaseAction(() => GameContext.Instance.GetAudioPool().Return(entity)));
        }
    }
}