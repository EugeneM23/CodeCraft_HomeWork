using Atomic.Entities;
using UnityEngine;

namespace Game.Gameplay
{
    public static class JumpUseCase
    {
        /// <summary>How far below the capsule's foot still counts as standing on something.</summary>
        private const float GROUNDED_SKIN = 0.08f;

        /// <summary>
        /// Layers that cannot hold the character up: itself (9 Character), and the things that fly
        /// around it — 11 ThrowItem, 12 Bullet, 13 Weapon. Layer names come from ProjectSettings/TagManager.asset.
        /// </summary>
        private const int GROUND_MASK = ~((1 << 9) | (1 << 11) | (1 << 12) | (1 << 13));

        public static bool Jump(IEntity entity)
        {
            if (!entity.HasJumpableTag())
                return false;

            if (!entity.GetJumpCondition().Invoke())
                return false;

            if (!IsGrounded(entity))
                return false;

            float jumpForce = entity.GetJumpForce();
            entity.GetRiggedBody().AddForce(new Vector3(0, jumpForce, 0), ForceMode.Impulse);
            entity.GetJumpEvent().Invoke();

            return true;
        }

        /// <summary>
        /// True when something solid is directly under the capsule's foot.
        /// Without this the impulse could be applied in mid-air, and repeated presses stacked
        /// upward velocity with nothing to bleed it off.
        /// </summary>
        public static bool IsGrounded(IEntity entity)
        {
            if (!entity.TryGetRiggedBody(out Rigidbody body))
                return false;

            // No capsule to probe with: do not block the jump.
            if (!body.TryGetComponent(out CapsuleCollider capsule))
                return true;

            float radius = capsule.radius;
            Vector3 localFoot = capsule.center + Vector3.down * Mathf.Max(0f, capsule.height * 0.5f - radius);
            Vector3 foot = capsule.transform.TransformPoint(localFoot);

            // A slightly narrower sphere, pushed just below the capsule's lowest point, so it does
            // not catch on the wall the character is leaning against.
            float probeRadius = radius * 0.9f;
            Vector3 probe = foot + Vector3.down * (radius - probeRadius + GROUNDED_SKIN);

            return Physics.CheckSphere(probe, probeRadius, GROUND_MASK, QueryTriggerInteraction.Ignore);
        }
    }
}
