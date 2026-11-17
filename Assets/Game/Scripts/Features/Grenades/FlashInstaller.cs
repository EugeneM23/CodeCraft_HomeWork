using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Makes a grenade a flash grenade: when its fuse runs out it blinds the enemies and dazzles the
    /// player in sight of it (FlashUseCase), with no damage, no destruction and no push. Put it next
    /// to GrenadeInstaller in place of ExplosionInstaller.
    /// </summary>
    public sealed class FlashInstaller : MonoEntityInstaller
    {
        [SerializeField] private float _radius = 8f;
        [Tooltip("Buff a blinded enemy gets (BlindBuffConfig).")]
        [SerializeField] private BuffConfig _blindBuff;
        [Tooltip("Pooled effect entity (HitEffectInstaller) spawned where the grenade goes off.")]
        [SerializeField] private MonoEntity _vfxPrefab;
        [SerializeField] private CameraShakeArgs _shake;

        public override void Install(IEntity entity)
        {
            entity.AddFlashRadius(_radius);

            if (_blindBuff != null)
                entity.AddFlashBlindBuff(_blindBuff);
            if (_vfxPrefab != null)
                entity.AddFlashVfxPrefab(_vfxPrefab);
            if (_shake != null && _shake.ShakeDuration > 0f)
                entity.AddCameraShakeArgs(_shake);
        }
    }
}
