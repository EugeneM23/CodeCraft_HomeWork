using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public class AudioSourceInstaller : MonoEntityInstaller
    {
        [SerializeField] private AudioSource _audioSource;

        public override void Install(IEntity entity)
        {
            entity.AddEntityID(GameFactoryID.AudioSource.ToString());
            entity.AddAudioSource(_audioSource);
            entity.AddLifeTime(new CooldownTimer(1f));
            entity.AddBehaviour<LifeTimeBehaviour>();
            entity.AddDestroyAction(new InlineAction(() =>
                GameContext.Instance.GetGameFactory().Destroy(entity)));
        }
    }
}