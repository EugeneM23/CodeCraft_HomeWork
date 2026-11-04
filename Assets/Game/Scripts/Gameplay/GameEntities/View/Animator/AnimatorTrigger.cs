using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>
    /// An animator trigger that fires only when the controller currently in use declares it.
    /// </summary>
    /// <remarks>
    /// The animator's controller is not fixed: SwitchAnimatorBehaviour replaces it with whatever
    /// the entity's current weapon carries. Of all the controllers in the project only AC_Zombie
    /// declares Attack and TakeDamage -- every weapon except Zombie_Hand carries an override of
    /// AC_BaseCharacter, which declares IsMoving, MoveX and MoveY and nothing more. So an enemy
    /// holding a rifle runs a controller without those triggers, and calling SetTrigger blind logs
    /// "Parameter 'Hash ...' does not exist" on every hit and every swing.
    ///
    /// The lookup is cached because it has to be recomputed whenever the controller is swapped,
    /// and it walks the parameters by index so that nothing is allocated per call -- the
    /// Animator.parameters array is a fresh copy on every access.
    /// </remarks>
    public sealed class AnimatorTrigger
    {
        private readonly int _hash;

        private RuntimeAnimatorController _inspected;
        private bool _declared;

        public AnimatorTrigger(string name) => _hash = Animator.StringToHash(name);

        public void Fire(Animator animator)
        {
            if (animator == null)
                return;

            if (this.IsDeclaredBy(animator))
                animator.SetTrigger(_hash);
        }

        private bool IsDeclaredBy(Animator animator)
        {
            RuntimeAnimatorController controller = animator.runtimeAnimatorController;

            if (controller == null)
                return false;

            if (ReferenceEquals(controller, _inspected))
                return _declared;

            int count = animator.parameterCount;

            // An animator that has not bound its controller yet reports no parameters at all.
            // Answering "not declared" would be right for this frame but wrong to remember, so
            // the result is left uncached until there is something to read.
            if (count == 0)
                return false;

            bool declared = false;

            for (int i = 0; i < count; i++)
            {
                AnimatorControllerParameter parameter = animator.GetParameter(i);

                if (parameter.nameHash != _hash) continue;
                if (parameter.type != AnimatorControllerParameterType.Trigger) continue;

                declared = true;
                break;
            }

            _inspected = controller;
            _declared = declared;

            return declared;
        }
    }
}
