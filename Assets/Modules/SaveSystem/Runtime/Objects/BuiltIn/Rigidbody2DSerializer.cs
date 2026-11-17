#if SAVE_PHYSICS_2D
using UnityEngine;

namespace Saving.Objects
{
    /// <summary>
    /// Built in: body type, simulated, body position and rotation, velocities, sleep, gravity scale, constraints.
    /// Contacts and solver state are not restored: a tiny jolt after a load is possible.
    /// </summary>
    public sealed class Rigidbody2DSerializer : ComponentSerializer<Rigidbody2D, Rigidbody2DSerializer.Data>
    {
        public sealed class Data
        {
            public RigidbodyType2D BodyType = RigidbodyType2D.Dynamic;
            public bool Simulated = true;
            public Vector2 Position;
            public float Rotation;
            public Vector2 LinearVelocity;
            public float AngularVelocity;
            public bool Awake = true;
            public float GravityScale = 1f;
            public RigidbodyConstraints2D Constraints;
        }

        protected override Data Serialize(Rigidbody2D body) => new()
        {
            BodyType = body.bodyType,
            Simulated = body.simulated,
            Position = body.position,
            Rotation = body.rotation,
            LinearVelocity = body.linearVelocity,
            AngularVelocity = body.angularVelocity,
            Awake = body.IsAwake(),
            GravityScale = body.gravityScale,
            Constraints = body.constraints
        };

        protected override void Deserialize(Rigidbody2D body, Data data)
        {
            body.bodyType = data.BodyType;
            body.simulated = data.Simulated;
            body.constraints = data.Constraints;
            body.gravityScale = data.GravityScale;
            body.position = data.Position;
            body.rotation = data.Rotation;
            if (body.bodyType != RigidbodyType2D.Static)
            {
                body.linearVelocity = data.LinearVelocity;
                body.angularVelocity = data.AngularVelocity;
            }

            if (data.Awake)
                body.WakeUp();
            else
                body.Sleep();
        }
    }
}
#endif
