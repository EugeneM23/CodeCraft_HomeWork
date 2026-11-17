using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Makes a projectile explosive. Put it next to BulletInstaller: BulletCollisionBehaviour then
    /// detonates the projectile on any contact instead of hitting a single target.
    /// </summary>
    public class ExplosionInstaller : MonoEntityInstaller
    {
        [SerializeField] private float _radius = 4f;
        [Tooltip("Share of the damage dealt at the very edge of the radius; full damage at the centre.")]
        [Range(0f, 1f)]
        [SerializeField] private float _minDamageFactor = 0.25f;
        [Tooltip("Pooled effect entity (HitEffectInstaller) spawned at the explosion point.")]
        [SerializeField] private MonoEntity _vfxPrefab;
        [Tooltip("Pooled spray of small crumbs (HitEffectInstaller + particle systems), aimed along the shot and tinted with the material of what was hit. Not spawned on characters.")]
        [SerializeField] private MonoEntity _crumbsPrefab;
        [SerializeField] private CameraShakeArgs _shake;

        public override void Install(IEntity entity)
        {
            entity.AddExplosionRadius(_radius);
            entity.AddExplosionMinDamageFactor(_minDamageFactor);

            if (_vfxPrefab != null)
                entity.AddExplosionVfxPrefab(_vfxPrefab);
            if (_crumbsPrefab != null)
                entity.AddExplosionCrumbsPrefab(_crumbsPrefab);
            if (_shake != null && _shake.ShakeDuration > 0f)
                entity.AddCameraShakeArgs(_shake);
        }
    }
}
