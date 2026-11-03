using System.Collections.Generic;
using System.Linq;
using Atomic.Entities;
using UnityEngine;

namespace Game.Gameplay
{
    public static class BuffUseCase
    {
        public static bool Apply(IEntity character, BuffBase buff)
        {
            IList<BuffBase> buffsEffects = character.GetBuffsEffects();

            if (buffsEffects.Any(b => b.Name == buff.Name))
                return false;

            buffsEffects.Add(buff);
            buff.Apply(character);

            return true;
        }

        public static bool CanApply(IEntity character, BuffConfig buff)
        {
            IList<BuffBase> buffsEffects = character.GetBuffsEffects();
            
            if (buffsEffects.Any(b => b.Name == buff.Name))
                return false;

                return true;
        }

        public static bool Discard(IEntity character, string buffName)
        {
            IList<BuffBase> buffsEffects = character.GetBuffsEffects();

            BuffBase buff = buffsEffects.FirstOrDefault(b => b.Name == buffName);

            if (buff != null)
            {
                buffsEffects.Remove(buff);
                buff.Discard(character);
                return true;
            }

            return false;
        }

        public static void DiscardAll(IEntity character)
        {
            IList<BuffBase> buffsEffects = character.GetBuffsEffects();

            foreach (var buff in buffsEffects)
                buff.Discard(character);

            buffsEffects.Clear();
        }
    }
}