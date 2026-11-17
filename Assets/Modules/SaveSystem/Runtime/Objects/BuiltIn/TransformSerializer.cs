using UnityEngine;

namespace Saving.Objects
{
    /// <summary>Built in: local position, rotation and scale. The parent stays as in the scene.</summary>
    public sealed class TransformSerializer : ComponentSerializer<Transform, TransformSerializer.Data>
    {
        public sealed class Data
        {
            public Vector3 LocalPosition;
            public Quaternion LocalRotation = Quaternion.identity;
            public Vector3 LocalScale = Vector3.one;
        }

        protected override Data Serialize(Transform transform) => new()
        {
            LocalPosition = transform.localPosition,
            LocalRotation = transform.localRotation,
            LocalScale = transform.localScale
        };

        protected override void Deserialize(Transform transform, Data data)
        {
            transform.localPosition = data.LocalPosition;
            transform.localRotation = data.LocalRotation;
            transform.localScale = data.LocalScale;
            TellMovers(transform);
        }

        /// <summary>The object was put somewhere by a load: the components that own its position are told.</summary>
        internal static void TellMovers(Transform transform)
        {
#if SAVE_PHYSICS
            // A CharacterController keeps its own copy of the position and its next Move puts the object back there
            // (Physics.autoSyncTransforms is off by default): switching it off and on makes it take the new one
            if (transform.TryGetComponent(out CharacterController controller) && controller.enabled)
            {
                controller.enabled = false;
                controller.enabled = true;
            }
#endif
#if SAVE_AI
            // A NavMeshAgent owns the position the same way and walks the object back on its next update: it is told
            // where it stands now (its path is the game's to give again)
            if (transform.TryGetComponent(out UnityEngine.AI.NavMeshAgent agent) && agent.enabled && agent.isOnNavMesh)
                agent.Warp(transform.position);
#endif
        }
    }
}
