using System.Collections.Generic;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Sets a flash grenade off: every enemy in the radius with a clear line to the flash is blinded
    /// (BlindBuff), the player in the same position gets FlashedEvent with the flash's strength,
    /// which whites the screen out. A wall between the flash and someone shields them.
    /// </summary>
    public static class FlashUseCase
    {
        // The flash sits this high above where the grenade lies, so the floor never blocks its sight.
        private const float FLASH_HEIGHT = 0.3f;
        // Strength at the very edge of the radius; full strength at the centre.
        private const float EDGE_STRENGTH = 0.3f;

        /// <summary>
        /// What does not shield anyone from the flash: pickups (6), dummies (7), enemies (8), the
        /// character (9), thrown items (11), bullets (12), weapons (13), debris (15), rubble (16) and
        /// Ignore Raycast (2). Walls, the floor and level geometry do. Layer names: TagManager.asset.
        /// </summary>
        private const int SIGHT_BLOCKERS = ~((1 << PhysicsLayers.IgnoreRaycast) | (1 << PhysicsLayers.Interactable) | (1 << PhysicsLayers.Dummy) | (1 << PhysicsLayers.Enemy) | (1 << PhysicsLayers.Character) |
                                             (1 << PhysicsLayers.ThrowItem) | (1 << PhysicsLayers.Bullet) | (1 << PhysicsLayers.Weapon) | (1 << PhysicsLayers.Debris) | (1 << PhysicsLayers.Rubble));

        public static bool IsFlash(IEntity grenade) => grenade.HasFlashRadius();

        public static void Flash(IEntity grenade, Vector3 point, GameFactory factory)
        {
            float radius = grenade.GetFlashRadius();
            Vector3 flash = point + Vector3.up * FLASH_HEIGHT;
            grenade.TryGetFlashBlindBuff(out BuffConfig blind);

            // A wall between the flash and someone shields them; another of their colliders in
            // sight still lets the flash through.
            BlastAreaUseCase.ForEachDamageable(point, radius,
                (target, hit) => InSight(flash, hit.bounds.center, target),
                (target, distanceShare) =>
                {
                    if (target.HasEnemyTag() && blind != null && target.HasBuffsEffects())
                        BuffUseCase.Apply(target, blind.CreateBuff());

                    if (target.HasPlayerTag() && target.TryGetFlashedEvent(out var flashed))
                        flashed.Invoke(Mathf.Lerp(1f, EDGE_STRENGTH, distanceShare));
                });

            if (grenade.TryGetFlashVfxPrefab(out MonoEntity vfxPrefab) && vfxPrefab != null)
            {
                IEntity vfx = factory.Create(vfxPrefab);
                vfx.GetTransform().SetPositionAndRotation(flash, Quaternion.identity);
            }

            if (grenade.TryGetCameraShakeArgs(out CameraShakeArgs shake))
                CameraShakeUseCase.ShakePlayerCamera(shake);
        }

        /// <summary>Nothing but the target itself between the flash and the target's middle.</summary>
        private static bool InSight(Vector3 flash, Vector3 point, IEntity target)
        {
            Vector3 offset = point - flash;
            float distance = offset.magnitude;

            if (distance < 0.001f)
                return true;

            return !Physics.Raycast(flash, offset / distance, out RaycastHit block, distance, SIGHT_BLOCKERS,
                       QueryTriggerInteraction.Ignore)
                   || BlastAreaUseCase.FindEntity(block.collider) == target;
        }
    }
}
