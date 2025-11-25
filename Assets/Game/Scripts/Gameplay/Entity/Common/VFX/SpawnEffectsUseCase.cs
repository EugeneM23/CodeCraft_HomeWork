using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public static class SpawnParticleUseCase
    {
        public static void SpawnEnviromentHit(Collision collision, IEntityPool pool)
        {
            ContactPoint contact = collision.GetContact(0);
            Vector3 position = contact.point;
            Vector3 normal = contact.normal;
        
            IEntity go = pool.Rent();
            Transform effectTransform = go.GetTransform();
            effectTransform.position = position;
            effectTransform.rotation = Quaternion.LookRotation(normal);
        }
    }
}