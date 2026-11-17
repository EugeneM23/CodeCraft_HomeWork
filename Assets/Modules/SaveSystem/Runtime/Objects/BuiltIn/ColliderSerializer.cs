#if SAVE_PHYSICS
using UnityEngine;

namespace Saving.Objects
{
    /// <summary>Built in: no data of its own; it lists 3D colliders in the Saveable so their enabled flag is kept.</summary>
    public sealed class ColliderSerializer : ComponentSerializer<Collider, ColliderSerializer.Data>
    {
        public sealed class Data
        {
        }

        protected override Data Serialize(Collider collider) => new();

        protected override void Deserialize(Collider collider, Data data)
        {
        }
    }
}
#endif
