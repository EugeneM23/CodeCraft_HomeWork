using Atomic.Entities;

namespace Game
{
    public static class InteractUseCase
    {
        public static void Interact(IEntity character, IEntity target)
        {
            if (target == null || !target.HasInteractableTag()) return;
            
            target.GetInteractAction().Invoke(character);
        }
    }
}