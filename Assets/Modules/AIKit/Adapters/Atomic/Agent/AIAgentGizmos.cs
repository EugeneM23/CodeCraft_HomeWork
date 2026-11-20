using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace AIKit.Entities
{
    /// <summary>
    /// The agent at a glance in the Scene view (an entity behaviour, as the Atomic guide's gizmos are): a line to what
    /// it is after (<c>AgentTarget</c>) and the way it walks this step (<c>AgentMove</c>). Added by the installer.
    /// </summary>
    public sealed class AIAgentGizmos : IEntityGizmos
    {
        private static readonly Color TargetColor = new(1f, 0.35f, 0.2f, 0.9f);
        private static readonly Color WalkColor = new(0.3f, 0.9f, 1f, 0.9f);

        public void DrawGizmos(IEntity entity)
        {
            if (entity is not Component component || component == null)
                return;

            Vector3 at = component.transform.position + Vector3.up;
            if (entity.TryGetAgentTarget(out IReactiveVariable<Transform> target) && target.Value != null)
            {
                Gizmos.color = TargetColor;
                Gizmos.DrawLine(at, target.Value.position + Vector3.up);
            }

            if (entity.TryGetAgentMove(out IReactiveVariable<Vector3> move) && move.Value != Vector3.zero)
            {
                Gizmos.color = WalkColor;
                Gizmos.DrawLine(at, at + move.Value * 2f);
            }
        }
    }
}
