using System.Collections.Generic;
using Atomic.Entities;

namespace Game.Gameplay
{
    public static class BuffUseCase
    {
        public static bool Apply(IEntity character, BaseBuff buff)
        {
            IList<BaseBuff> buffsEffects = character.GetBuffsEffects();

            if (buffsEffects.Contains(buff))
                return false;

            buffsEffects.Add(buff);
            buff.Apply(character);

            return true;
        }

        public static bool Discard(IEntity character, BaseBuff buff)
        {
            IList<BaseBuff> buffsEffects = character.GetBuffsEffects();

            if (!buffsEffects.Remove(buff))
                return false;

            buff.Discard(character);

            return true;
        }

        public static void DiscardAll(IEntity character)
        {
            IList<BaseBuff> buffsEffects = character.GetBuffsEffects();

            foreach (var buff in buffsEffects)
                buff.Discard(character);

            buffsEffects.Clear();
        }
    }
}