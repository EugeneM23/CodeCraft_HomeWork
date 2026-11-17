#if SAVE_PHYSICS_2D
using UnityEngine;

namespace Saving.Objects
{
    /// <summary>Built in: no data of its own; it lists 2D colliders in the Saveable so their enabled flag is kept.</summary>
    public sealed class Collider2DSerializer : ComponentSerializer<Collider2D, Collider2DSerializer.Data>
    {
        public sealed class Data
        {
        }

        protected override Data Serialize(Collider2D collider) => new();

        protected override void Deserialize(Collider2D collider, Data data)
        {
        }
    }
}
#endif
