using Atomic.Entities;
using Modules.Gameplay;
using UnityEngine;

namespace Game
{
    public static class SpawnUseCase
    {
        public static void SpawnCollisionHit(Collision collision, GameFactory gameFactory, IEntity bullet)
        {
            ContactPoint contact = collision.GetContact(0);
            Vector3 position = contact.point;
            Vector3 normal = contact.normal;

            IEntity go = gameFactory.Create(bullet.GetBulletHitPrefab());
            go.GetLifeTime().Reset();
            Transform effectTransform = go.GetTransform();
            effectTransform.position = position;
            effectTransform.rotation = Quaternion.LookRotation(normal);
        }


        public static IEntity SpawnBullet(IEntity weapon, GameFactory gameFactory, Transform firePoint)
        {
            IEntity bullet = gameFactory.Create(weapon.GetBulletPrefab());

            bullet.AddDamage(weapon.GetDamage());
            bullet.GetTransform().SetPositionAndRotation(firePoint.position, firePoint.rotation);
            bullet.GetMoveDirection().Value = bullet.GetTransform().forward;
            bullet.GetLifeTime().Reset();

            return bullet;
        }

        public static void SpawnShell(GameFactory gameFactory, IEntity weapon, Transform shellPoint, float impulse)
        {
            IEntity shell = gameFactory.Create(weapon.GetShellPrefab());
            shell.GetTransform().SetPositionAndRotation(shellPoint.position, shellPoint.rotation);
            shell.GetLifeTime().Reset();
            shell.GetRiggedBody().AddForce((Vector3.up + shellPoint.right) * impulse, ForceMode.Impulse);
        }

        public static void UnSpawnEntity(in GameFactory factory, in IEntity entity)
        {
            if (entity.TryGetLifeTime(out Cooldown lifeTime))
                lifeTime.Reset();

            factory.Destroy(entity);
        }
    }
}