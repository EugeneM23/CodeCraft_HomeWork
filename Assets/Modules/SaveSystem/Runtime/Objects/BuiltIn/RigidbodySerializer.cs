#if SAVE_PHYSICS
using UnityEngine;

namespace Saving.Objects
{
    /// <summary>Built in: kinematic, gravity, velocities, sleep, constraints. The pose comes from Transform.</summary>
    public sealed class RigidbodySerializer : ComponentSerializer<Rigidbody, RigidbodySerializer.Data>
    {
        public sealed class Data
        {
            public bool IsKinematic;
            public bool UseGravity = true;
            public Vector3 LinearVelocity;
            public Vector3 AngularVelocity;
            public bool Sleeping;
            public RigidbodyConstraints Constraints;
        }

        protected override Data Serialize(Rigidbody body) => new()
        {
            IsKinematic = body.isKinematic,
            UseGravity = body.useGravity,
            LinearVelocity = body.linearVelocity,
            AngularVelocity = body.angularVelocity,
            Sleeping = body.IsSleeping(),
            Constraints = body.constraints
        };

        protected override void Deserialize(Rigidbody body, Data data)
        {
            body.isKinematic = data.IsKinematic;
            body.useGravity = data.UseGravity;
            body.constraints = data.Constraints;
            // A kinematic body takes no velocity (Unity warns)
            if (!body.isKinematic)
            {
                body.linearVelocity = data.LinearVelocity;
                body.angularVelocity = data.AngularVelocity;
            }

            if (data.Sleeping)
                body.Sleep();
            else
                body.WakeUp();
        }
    }
}
#endif
