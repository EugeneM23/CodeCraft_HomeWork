using Atomic.Entities;
using UnityEngine;

namespace Game.Gameplay
{
    public static class JumpUseCase
    {
        public static bool Jump(IEntity entity)
        {
            if (!entity.HasJumpableTag())
                return false;

            if (!entity.GetJumpCondition().Invoke())
                return false;

            float jumpForce = entity.GetJumpForce();
            entity.GetRiggedBody().AddForce(new Vector3(0, jumpForce, 0), ForceMode.Impulse);
            entity.GetJumpEvent().Invoke();

            return true;
        }
    }
}